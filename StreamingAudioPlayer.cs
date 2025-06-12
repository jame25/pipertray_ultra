using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace PiperTray
{
    public class StreamingAudioPlayer : IDisposable
    {
        private readonly ConcurrentQueue<byte[]> audioQueue = new();
        private WaveOutEvent? waveOut;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? playbackTask;
        private bool disposed = false;

        public async Task PlayStreamingAsync(string text, Func<string, Task<byte[][]>> generateChunksAsync)
        {
            Stop();
            
            cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // Start audio generation and playback concurrently
            var generationTask = GenerateAudioChunksAsync(text, generateChunksAsync, cancellationToken);
            playbackTask = PlayQueuedAudioAsync(cancellationToken);

            await Task.WhenAll(generationTask, playbackTask);
        }

        private async Task GenerateAudioChunksAsync(string text, Func<string, Task<byte[][]>> generateChunksAsync, CancellationToken cancellationToken)
        {
            try
            {
                var audioChunks = await generateChunksAsync(text);
                
                foreach (var chunk in audioChunks)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                        
                    audioQueue.Enqueue(chunk);
                }
            }
            catch (Exception)
            {
                // Generation failed, mark completion by enqueueing empty array
                audioQueue.Enqueue(Array.Empty<byte>());
            }
            finally
            {
                // Signal completion
                audioQueue.Enqueue(null);
            }
        }

        private async Task PlayQueuedAudioAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (audioQueue.TryDequeue(out var audioData))
                    {
                        // null signals completion
                        if (audioData == null)
                            break;
                            
                        // Empty array signals error
                        if (audioData.Length == 0)
                            break;

                        PlayAudioChunk(audioData);
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            }, cancellationToken);
        }

        private void PlayAudioChunk(byte[] audioData)
        {
            try
            {
                using var memoryStream = new MemoryStream(audioData);
                using var waveReader = new WaveFileReader(memoryStream);
                
                var tempWaveOut = new WaveOutEvent();
                tempWaveOut.Init(waveReader);
                tempWaveOut.Play();

                // Wait for this chunk to complete
                while (tempWaveOut.PlaybackState == PlaybackState.Playing)
                {
                    Thread.Sleep(10);
                }

                tempWaveOut.Dispose();
            }
            catch (Exception)
            {
                // Audio playback failed for this chunk, skip it
            }
        }

        public void Stop()
        {
            cancellationTokenSource?.Cancel();
            waveOut?.Stop();
            waveOut?.Dispose();
            
            // Clear the queue
            while (audioQueue.TryDequeue(out _)) { }
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
                    Stop();
                    cancellationTokenSource?.Dispose();
                }
                disposed = true;
            }
        }

        ~StreamingAudioPlayer()
        {
            Dispose(false);
        }
    }
}