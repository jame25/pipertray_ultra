using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace PiperTray
{
    public class AudioPlayer : IDisposable
    {
        private WaveOutEvent? waveOut;
        private WaveFileReader? waveReader;
        private MemoryStream? memoryStream;
        private bool disposed = false;
        private bool isPaused = false;

        public bool IsPaused => isPaused;
        public bool IsPlaying => waveOut?.PlaybackState == PlaybackState.Playing;
        public TimeSpan CurrentPosition => waveReader?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalDuration => waveReader?.TotalTime ?? TimeSpan.Zero;

        public void Stop()
        {
            waveOut?.Stop();
            waveOut?.Dispose();
            waveOut = null;
            
            waveReader?.Dispose();
            waveReader = null;
            
            memoryStream?.Dispose();
            memoryStream = null;
            
            isPaused = false;
        }

        public void Pause()
        {
            if (waveOut?.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
                isPaused = true;
                Logger.Info("Audio playback paused");
            }
        }

        public void Resume()
        {
            if (waveOut?.PlaybackState == PlaybackState.Paused)
            {
                waveOut.Play();
                isPaused = false;
                Logger.Info("Audio playback resumed");
            }
        }

        public void SkipForward(int seconds)
        {
            if (waveReader != null && (IsPlaying || IsPaused))
            {
                var newPosition = waveReader.CurrentTime.Add(TimeSpan.FromSeconds(seconds));
                if (newPosition <= waveReader.TotalTime)
                {
                    waveReader.CurrentTime = newPosition;
                    Logger.Info($"Skipped forward {seconds} seconds to {newPosition.TotalSeconds:F1}s");
                }
                else
                {
                    waveReader.CurrentTime = waveReader.TotalTime;
                    Logger.Info($"Skipped to end of audio at {waveReader.TotalTime.TotalSeconds:F1}s");
                }
            }
        }

        public void SkipBack(int seconds)
        {
            if (waveReader != null && (IsPlaying || IsPaused))
            {
                var newPosition = waveReader.CurrentTime.Subtract(TimeSpan.FromSeconds(seconds));
                if (newPosition >= TimeSpan.Zero)
                {
                    waveReader.CurrentTime = newPosition;
                    Logger.Info($"Skipped back {seconds} seconds to {newPosition.TotalSeconds:F1}s");
                }
                else
                {
                    waveReader.CurrentTime = TimeSpan.Zero;
                    Logger.Info($"Skipped to beginning of audio");
                }
            }
        }

        public async Task PlayAsync(byte[] audioData, CancellationToken cancellationToken = default)
        {
            if (audioData.Length == 0)
            {
                Logger.Warn("PlayAsync called with empty audio data");
                return;
            }

            Logger.Info($"Starting audio playback - {audioData.Length} bytes");

            await Task.Run(() =>
            {
                try
                {
                    // Stop any currently playing audio
                    Stop();

                    // Check if cancelled before starting
                    cancellationToken.ThrowIfCancellationRequested();

                    // Create a memory stream from the WAV audio data
                    memoryStream = new MemoryStream(audioData);
                    waveReader = new WaveFileReader(memoryStream);

                    Logger.Debug($"WAV format: {waveReader.WaveFormat.SampleRate}Hz, {waveReader.WaveFormat.BitsPerSample}-bit, {waveReader.WaveFormat.Channels} channels");
                    Logger.Debug($"Duration: {waveReader.TotalTime.TotalSeconds:F2} seconds");

                    // Initialize audio output
                    waveOut = new WaveOutEvent();
                    waveOut.Init(waveReader);
                    waveOut.Play();
                    isPaused = false;

                    Logger.Debug("Audio playback started");

                    // Wait for playback to complete or cancellation
                    while (waveOut != null && (waveOut.PlaybackState == PlaybackState.Playing || waveOut.PlaybackState == PlaybackState.Paused))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Logger.Debug("Audio playback cancelled");
                            break;
                        }
                        System.Threading.Thread.Sleep(10);
                    }

                    Logger.Debug("Audio playback finished");

                    // Only dispose if not paused - we keep them for resume
                    if (!isPaused)
                    {
                        waveOut.Dispose();
                        waveOut = null;
                        waveReader.Dispose();
                        waveReader = null;
                        memoryStream.Dispose();
                        memoryStream = null;
                    }
                    
                    // Throw cancellation exception if cancelled
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("Audio playback was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error("Audio playback failed", ex);
                }
            }, cancellationToken);
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
                    waveOut?.Stop();
                    waveOut?.Dispose();
                    waveReader?.Dispose();
                    memoryStream?.Dispose();
                }
                disposed = true;
            }
        }

        ~AudioPlayer()
        {
            Dispose(false);
        }
    }
}