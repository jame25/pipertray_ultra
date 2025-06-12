using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace PiperTray
{
    public class PiperTtsEngine : IDisposable
    {
        private readonly string piperExecutablePath;
        private readonly string modelPath;
        private Process? piperProcess;
        private readonly SemaphoreSlim requestSemaphore = new(1, 1);
        private bool disposed = false;

        public PiperTtsEngine()
        {
            piperExecutablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper-executable", "piper.exe");
            modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper-executable", "en_us-jane-medium.onnx");
        }

        public async Task InitializeAsync()
        {
            if (!File.Exists(piperExecutablePath))
                throw new FileNotFoundException($"Piper executable not found at: {piperExecutablePath}");
            
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Voice model not found at: {modelPath}");

            await StartPersistentProcessAsync();
        }

        private async Task StartPersistentProcessAsync()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = piperExecutablePath,
                Arguments = $"--model \"{modelPath}\" --length-scale 1.0 --sentence-silence 0.2",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(piperExecutablePath)
            };

            piperProcess = new Process { StartInfo = startInfo };
            piperProcess.Start();

            // Give the process time to load the model
            await Task.Delay(1000);

            if (piperProcess.HasExited)
            {
                string error = await piperProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"Piper process failed to start: {error}");
            }
        }

        public async Task<byte[]> GenerateSpeechAsync(string text)
        {
            // For short text, use direct processing
            if (text.Length < 500)
            {
                return await GenerateAudioChunkAsync(text);
            }

            // For long text, use streaming approach
            return await GenerateStreamingSpeechAsync(text);
        }

        public async Task<byte[][]> GenerateStreamingChunksAsync(string text)
        {
            var chunks = SplitIntoChunks(text);
            
            if (chunks.Count == 1)
            {
                var singleAudio = await GenerateAudioChunkAsync(chunks[0]);
                return new[] { singleAudio };
            }

            // Process first chunk immediately
            var firstChunkTask = GenerateAudioChunkAsync(chunks[0]);
            
            // Start processing remaining chunks in parallel (limited concurrency)
            var semaphore = new SemaphoreSlim(3, 3); // Max 3 concurrent processes
            var remainingChunkTasks = chunks.Skip(1).Select(async chunk =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await GenerateAudioChunkAsync(chunk);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            // Wait for first chunk
            var firstAudio = await firstChunkTask;
            
            // Return first chunk immediately, then remaining chunks as they complete
            var results = new List<byte[]> { firstAudio };
            
            // Wait for remaining chunks
            var remainingAudio = await Task.WhenAll(remainingChunkTasks);
            results.AddRange(remainingAudio);
            
            return results.ToArray();
        }

        private async Task<byte[]> GenerateStreamingSpeechAsync(string text)
        {
            // Split text into manageable chunks (paragraphs or sentences)
            var chunks = SplitIntoChunks(text);
            
            if (chunks.Count == 1)
            {
                return await GenerateAudioChunkAsync(chunks[0]);
            }

            // Process first chunk immediately for instant playback
            var firstChunkTask = GenerateAudioChunkAsync(chunks[0]);
            
            // Start processing remaining chunks in parallel
            var remainingChunkTasks = chunks.Skip(1)
                .Select(chunk => GenerateAudioChunkAsync(chunk))
                .ToArray();

            // Wait for first chunk to complete
            var firstAudio = await firstChunkTask;
            
            // Wait for all remaining chunks
            var remainingAudio = await Task.WhenAll(remainingChunkTasks);
            
            // Combine all audio chunks
            return CombineAudioChunks(new[] { firstAudio }.Concat(remainingAudio).ToArray());
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

        private async Task<byte[]> GenerateAudioChunkAsync(string text)
        {
            var tempFile = Path.GetTempFileName() + ".wav";
            
            try
            {
                using var requestProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = piperExecutablePath,
                        Arguments = $"--model \"{modelPath}\" --output_file \"{tempFile}\"",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(piperExecutablePath)
                    }
                };

                requestProcess.Start();
                await requestProcess.StandardInput.WriteLineAsync(text);
                requestProcess.StandardInput.Close();

                var timeoutMs = Math.Max(3000, text.Length * 15); // Faster timeout for chunks
                if (!requestProcess.WaitForExit(timeoutMs))
                {
                    requestProcess.Kill();
                    throw new TimeoutException($"Piper chunk timed out after {timeoutMs}ms");
                }

                if (File.Exists(tempFile))
                {
                    return await File.ReadAllBytesAsync(tempFile);
                }
                else
                {
                    throw new InvalidOperationException("Piper failed to generate audio chunk");
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        private byte[] CombineAudioChunks(byte[][] audioChunks)
        {
            if (audioChunks.Length == 1)
                return audioChunks[0];

            // For WAV files, we need to combine them properly
            // For now, return the first chunk and let the streaming player handle the rest
            // A proper implementation would merge WAV headers and data
            return audioChunks[0];
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
                        if (piperProcess != null && !piperProcess.HasExited)
                        {
                            piperProcess.StandardInput.Close();
                            if (!piperProcess.WaitForExit(1000))
                                piperProcess.Kill();
                        }
                        piperProcess?.Dispose();
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

        ~PiperTtsEngine()
        {
            Dispose(false);
        }
    }
}