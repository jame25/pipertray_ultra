using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;

namespace PiperTray
{
    public class OptimizedPiperEngine : IDisposable
    {
        private readonly string piperExecutablePath;
        private readonly string modelsDirectory;
        private readonly SemaphoreSlim requestSemaphore = new(1, 1);
        private bool disposed = false;

        public OptimizedPiperEngine()
        {
            // Use the piper.exe from the piper-executable directory
            piperExecutablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper-executable", "piper.exe");
            modelsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper-executable");
        }

        public async Task InitializeAsync()
        {
            Logger.Info($"Initializing OptimizedPiperEngine");
            Logger.Info($"Piper executable path: {piperExecutablePath}");
            Logger.Info($"Models directory: {modelsDirectory}");

            if (!File.Exists(piperExecutablePath))
            {
                Logger.Error($"Optimized Piper executable not found at: {piperExecutablePath}");
                throw new FileNotFoundException($"Optimized Piper executable not found at: {piperExecutablePath}");
            }
            
            if (!Directory.Exists(modelsDirectory))
            {
                Logger.Error($"Models directory not found at: {modelsDirectory}");
                throw new DirectoryNotFoundException($"Models directory not found at: {modelsDirectory}");
            }

            var espeakDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper-executable", "espeak-ng-data");
            Logger.Info($"eSpeak data path: {espeakDataPath}");
            Logger.Info($"eSpeak data exists: {Directory.Exists(espeakDataPath)}");

            if (!Directory.Exists(espeakDataPath))
            {
                Logger.Error($"eSpeak-ng data directory not found at: {espeakDataPath}");
                throw new DirectoryNotFoundException($"eSpeak-ng data directory not found at: {espeakDataPath}");
            }

            Logger.Info("OptimizedPiperEngine initialized successfully");
            await Task.CompletedTask;
        }


        public async Task<byte[]> GenerateSpeechAsync(string text, int speed = 5, string voiceModel = "", int speakerId = 0)
        {
            Logger.Info($"GenerateSpeechAsync called with text length: {text.Length}, speed: {speed}, model: {voiceModel}, speaker: {speakerId}");
            
            // For short text, use direct processing
            if (text.Length <= 500)
            {
                return await GenerateSingleChunkAsync(text, speed, voiceModel, speakerId);
            }

            // For long text, use parallel chunked processing
            return await GenerateParallelChunksAsync(text, speed, voiceModel, speakerId);
        }

        private async Task<byte[]> GenerateSingleChunkAsync(string text, int speed = 5, string voiceModel = "", int speakerId = 0)
        {
            await requestSemaphore.WaitAsync();
            try
            {
                // Get the current settings to determine voice model and speaker
                var settings = AppSettings.Load();
                var actualVoiceModel = !string.IsNullOrEmpty(voiceModel) ? voiceModel : settings.DefaultVoice;
                var actualSpeed = speed > 0 ? speed : settings.DefaultSpeed;
                
                // If we have an active preset, use its model and speaker
                if (settings.CurrentActivePreset >= 0 && settings.CurrentActivePreset < settings.VoicePresets.Length)
                {
                    var currentPreset = settings.VoicePresets[settings.CurrentActivePreset];
                    if (currentPreset.Enabled)
                    {
                        actualVoiceModel = currentPreset.Model;
                        speakerId = currentPreset.SpeakerId;
                    }
                }
                
                return await GenerateWithModelAsync(text, actualVoiceModel, speakerId, actualSpeed);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in GenerateSingleChunkAsync", ex);
                throw;
            }
            finally
            {
                requestSemaphore.Release();
            }
        }

        private async Task<byte[]> GenerateParallelChunksAsync(string text, int speed = 5, string voiceModel = "", int speakerId = 0)
        {
            Logger.Info($"Using parallel processing for {text.Length} character text");
            
            // Split text into manageable chunks
            var chunks = SplitIntoChunks(text);
            Logger.Info($"Split text into {chunks.Count} chunks");
            
            if (chunks.Count == 1)
            {
                return await GenerateSingleChunkAsync(chunks[0], speed, voiceModel, speakerId);
            }

            // Process first chunk immediately for instant playback
            var firstChunkTask = GenerateSingleChunkAsync(chunks[0], speed, voiceModel, speakerId);
            
            // Start processing remaining chunks in parallel (limited concurrency)
            var semaphore = new SemaphoreSlim(3, 3); // Max 3 concurrent requests
            var remainingChunkTasks = chunks.Skip(1).Select(async chunk =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await GenerateSingleChunkAsync(chunk, speed, voiceModel, speakerId);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            // Wait for first chunk to complete for immediate playback
            var firstAudio = await firstChunkTask;
            Logger.Info($"First chunk completed: {firstAudio.Length} bytes");
            
            // Wait for all remaining chunks
            var remainingAudio = await Task.WhenAll(remainingChunkTasks);
            Logger.Info($"All remaining chunks completed");
            
            // Combine all audio chunks into a single WAV file
            var combinedAudio = CombineWavFiles(new[] { firstAudio }.Concat(remainingAudio).ToArray());
            Logger.Info($"Combined audio: {combinedAudio.Length} bytes total");
            
            return combinedAudio;
        }

        private async Task<byte[]> GenerateWithModelAsync(string text, string voiceModel, int speakerId, int speed)
        {
            Logger.Info($"Generating speech with model: {voiceModel}, speaker: {speakerId}, speed: {speed}");
            
            // Build the model path
            var modelPath = Path.Combine(modelsDirectory, $"{voiceModel}.onnx");
            if (!File.Exists(modelPath))
            {
                Logger.Error($"Voice model not found: {modelPath}");
                throw new FileNotFoundException($"Voice model not found: {modelPath}");
            }
            
            var espeakDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper-executable", "espeak-ng-data");
            
            // Build arguments with model, speaker, and speed
            var arguments = $"--model \"{modelPath}\" --espeak_data \"{espeakDataPath}\" --output-raw";
            
            // Add speaker parameter if not default
            if (speakerId > 0)
            {
                arguments += $" --speaker {speakerId}";
            }
            
            // Convert speed (1-10) to length scale (use culture-invariant formatting to avoid decimal issues)
            // Speed 1 = 1.5 (very slow), Speed 10 = 0.5 (very fast), Speed 5 ≈ 1.06 (normal)
            var lengthScale = 1.5 - ((speed - 1) / 9.0);
            var lengthScaleStr = lengthScale.ToString("F2", CultureInfo.InvariantCulture);
            arguments += $" --length_scale {lengthScaleStr}";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = piperExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(piperExecutablePath),
                StandardInputEncoding = System.Text.Encoding.UTF8
            };
            
            // Set environment variable for eSpeak-ng data path
            startInfo.EnvironmentVariables["ESPEAK_DATA_PATH"] = espeakDataPath;
            
            Logger.Debug($"Command: {startInfo.FileName} {startInfo.Arguments}");
            
            try
            {
                using var process = new Process { StartInfo = startInfo };
                
                // Set up stderr monitoring
                var stderrOutput = new List<string>();
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        stderrOutput.Add(args.Data);
                        Logger.Debug($"Piper stderr: {args.Data}");
                    }
                };
                
                process.Start();
                process.BeginErrorReadLine();
                
                // Send text to stdin with proper UTF-8 encoding
                Logger.Info($"Sending text to Piper: '{text.Substring(0, Math.Min(50, text.Length))}...' (length: {text.Length})");
                await process.StandardInput.WriteAsync(text);
                await process.StandardInput.FlushAsync();
                process.StandardInput.Close();
                
                // Read all output
                using var outputStream = new MemoryStream();
                await process.StandardOutput.BaseStream.CopyToAsync(outputStream);
                
                // Wait for process to complete
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    var errorMsg = string.Join("\n", stderrOutput);
                    Logger.Error($"Piper process failed with exit code {process.ExitCode}: {errorMsg}");
                    throw new InvalidOperationException($"Piper process failed: {errorMsg}");
                }
                
                var audioData = outputStream.ToArray();
                Logger.Info($"Generated {audioData.Length} bytes of raw PCM audio");
                
                // Convert raw PCM to WAV format
                var wavData = ConvertPcmToWav(audioData);
                Logger.Info($"Converted to WAV format: {wavData.Length} bytes");
                
                return wavData;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating speech with model {voiceModel}", ex);
                throw;
            }
        }

        private List<string> SplitIntoChunks(string text)
        {
            var chunks = new List<string>();
            
            // Split by paragraphs first (double newlines)
            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length <= 800)
                {
                    chunks.Add(paragraph.Trim());
                }
                else
                {
                    // Split long paragraphs by sentences
                    var sentences = Regex.Split(paragraph, @"(?<=[.!?])\s+")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();
                    
                    var currentChunk = "";
                    foreach (var sentence in sentences)
                    {
                        if (currentChunk.Length + sentence.Length <= 800)
                        {
                            currentChunk += (currentChunk.Length > 0 ? " " : "") + sentence;
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(currentChunk))
                                chunks.Add(currentChunk.Trim());
                            currentChunk = sentence;
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(currentChunk))
                        chunks.Add(currentChunk.Trim());
                }
            }
            
            return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }

        private byte[] CombineWavFiles(byte[][] wavFiles)
        {
            if (wavFiles.Length == 1)
                return wavFiles[0];

            // For proper WAV combination, we need to:
            // 1. Extract PCM data from each WAV file
            // 2. Concatenate the PCM data
            // 3. Create a new WAV header with the combined length

            var combinedPcmData = new List<byte>();
            
            foreach (var wavFile in wavFiles)
            {
                // Skip WAV header (44 bytes) and extract PCM data
                if (wavFile.Length > 44)
                {
                    var pcmData = new byte[wavFile.Length - 44];
                    Array.Copy(wavFile, 44, pcmData, 0, pcmData.Length);
                    combinedPcmData.AddRange(pcmData);
                }
            }

            // Create new WAV file with combined PCM data
            return ConvertPcmToWav(combinedPcmData.ToArray());
        }

        private byte[] ConvertPcmToWav(byte[] pcmData)
        {
            // WAV header for 22050 Hz, 16-bit, mono PCM
            const int sampleRate = 22050;
            const short bitsPerSample = 16;
            const short channels = 1;
            const int byteRate = sampleRate * channels * bitsPerSample / 8;
            const short blockAlign = (short)(channels * bitsPerSample / 8);

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            // RIFF header
            writer.Write("RIFF".ToCharArray());
            writer.Write((uint)(36 + pcmData.Length)); // File size - 8
            writer.Write("WAVE".ToCharArray());

            // Format chunk
            writer.Write("fmt ".ToCharArray());
            writer.Write((uint)16); // Chunk size
            writer.Write((short)1); // Audio format (PCM)
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            // Data chunk
            writer.Write("data".ToCharArray());
            writer.Write((uint)pcmData.Length);
            writer.Write(pcmData);

            return memoryStream.ToArray();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    try
                    {
                        requestSemaphore.Dispose();
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
                disposed = true;
            }
        }

        ~OptimizedPiperEngine()
        {
            Dispose(false);
        }
    }
}
