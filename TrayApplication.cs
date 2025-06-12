using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace PiperTray
{
    public class TrayApplication
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip contextMenu;
        private ClipboardMonitor clipboardMonitor;
        private OptimizedPiperEngine ttsEngine;
        private AudioPlayer audioPlayer;
        private CancellationTokenSource? currentSpeechCancellation;
        private int currentSpeed = 5;
        private AppSettings settings;
        private bool isMonitoringEnabled = true;
        private bool isSpeechPaused = false;
        private GlobalHotkeyManager? hotkeyManager;
        private string? lastReadClipboardText = null;
        
        // Animation fields
        private System.Windows.Forms.Timer? animationTimer;
        private Icon[]? animationFrames;
        private int currentFrame = 0;
        private Icon? originalIcon;

        public TrayApplication()
        {
            Logger.ClearLog(); // Start fresh
            Logger.Info("=== PiperTray Application Starting ===");
            
            // Load settings
            settings = AppSettings.Load();
            Logger.IsEnabled = settings.EnableLogging;
            currentSpeed = settings.DefaultSpeed;
            
            InitializeTrayIcon();
            InitializeAnimation();
            InitializeComponents();
            InitializeHotkeys();
            StartServices();
            
            // Set initial menu selections and states
            UpdateSpeedMenuSelection();
            UpdateVoiceMenuSelection();
            UpdateMonitoringMenuText();
            UpdatePauseMenuText();
        }

        private void InitializeTrayIcon()
        {
            CreateContextMenu();

            trayIcon = new NotifyIcon()
            {
                Icon = CreateTrayIcon(),
                ContextMenuStrip = contextMenu,
                Visible = true,
                Text = "PiperTray - Ready"
            };
        }
        
        private void CreateContextMenu()
        {
            contextMenu = new ContextMenuStrip();
            
            // Add menu items based on settings
            if (settings.ShowStopSpeechMenuItem)
            {
                contextMenu.Items.Add("Stop Speech", null, OnStopSpeech);
            }
            
            if (settings.ShowPauseResumeMenuItem)
            {
                contextMenu.Items.Add("Pause", null, OnPauseResume);
            }
            
            // Skip controls submenu
            if (settings.ShowSkipMenuItem)
            {
                var skipMenu = new ToolStripMenuItem("Skip");
                skipMenu.DropDownItems.Add($"← Back {settings.SkipIntervalSeconds}s", null, OnSkipBack);
                skipMenu.DropDownItems.Add($"Forward {settings.SkipIntervalSeconds}s →", null, OnSkipForward);
                contextMenu.Items.Add(skipMenu);
            }
            
            // Add separator if any of the above items were added
            if (settings.ShowStopSpeechMenuItem || settings.ShowPauseResumeMenuItem || settings.ShowSkipMenuItem)
            {
                contextMenu.Items.Add("-");
            }
            
            // Monitoring toggle
            if (settings.ShowMonitoringMenuItem)
            {
                contextMenu.Items.Add("Monitoring", null, OnToggleMonitoring);
                contextMenu.Items.Add("-");
            }
            
            // Speed submenu
            if (settings.ShowSpeedMenuItem)
            {
                var speedMenu = new ToolStripMenuItem("Speed");
                speedMenu.DropDownItems.Add("1 (Very Slow)", null, (s, e) => SetSpeed(1));
                speedMenu.DropDownItems.Add("2", null, (s, e) => SetSpeed(2));
                speedMenu.DropDownItems.Add("3", null, (s, e) => SetSpeed(3));
                speedMenu.DropDownItems.Add("4", null, (s, e) => SetSpeed(4));
                speedMenu.DropDownItems.Add("5 (Normal)", null, (s, e) => SetSpeed(5));
                speedMenu.DropDownItems.Add("6", null, (s, e) => SetSpeed(6));
                speedMenu.DropDownItems.Add("7", null, (s, e) => SetSpeed(7));
                speedMenu.DropDownItems.Add("8 (Fast)", null, (s, e) => SetSpeed(8));
                speedMenu.DropDownItems.Add("9", null, (s, e) => SetSpeed(9));
                speedMenu.DropDownItems.Add("10 (Very Fast)", null, (s, e) => SetSpeed(10));
                contextMenu.Items.Add(speedMenu);
            }
            
            // Voice submenu
            if (settings.ShowVoiceMenuItem)
            {
                var voiceMenu = new ToolStripMenuItem("Voice");
                
                // Get available voice models and add them to the menu
                var availableModels = VoiceModelDetector.GetAvailableVoiceModels();
                foreach (var model in availableModels)
                {
                    var displayName = VoiceModelDetector.GetDisplayName(model);
                    voiceMenu.DropDownItems.Add(displayName, null, (s, e) => SetVoice(model));
                }
                
                // If no models found, add a disabled placeholder
                if (availableModels.Count == 0)
                {
                    var noModelsItem = new ToolStripMenuItem("No voice models found")
                    {
                        Enabled = false
                    };
                    voiceMenu.DropDownItems.Add(noModelsItem);
                }
                
                contextMenu.Items.Add(voiceMenu);
            }
            
            // Presets submenu - only show if there are enabled presets
            if (settings.ShowPresetsMenuItem)
            {
                var enabledPresets = GetEnabledPresets();
                if (enabledPresets.Count > 0)
                {
                    var presetsMenu = new ToolStripMenuItem("Presets");
                    
                    for (int i = 0; i < enabledPresets.Count; i++)
                    {
                        var preset = enabledPresets[i];
                        var presetIndex = Array.IndexOf(settings.VoicePresets, preset);
                        presetsMenu.DropDownItems.Add(preset.Name, null, (s, e) => SwitchToPreset(presetIndex));
                    }
                    
                    contextMenu.Items.Add(presetsMenu);
                }
            }
            
            // Add separator before export if speed, voice, or presets items were added
            var hasEnabledPresets = settings.ShowPresetsMenuItem && GetEnabledPresets().Count > 0;
            if (settings.ShowSpeedMenuItem || settings.ShowVoiceMenuItem || hasEnabledPresets)
            {
                contextMenu.Items.Add("-");
            }
            
            if (settings.ShowExportToWavMenuItem)
            {
                contextMenu.Items.Add("Export to WAV", null, OnExportToWav);
                contextMenu.Items.Add("-");
            }
            
            contextMenu.Items.Add("Settings", null, OnSettings);
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, OnExit);
        }

        private Icon CreateTrayIcon()
        {
            // Use programmatically created icon with 3 vertical lines design
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                
                // Enable anti-aliasing for smoother lines
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                
                // Create the same 3 vertical lines as the animation (static version)
                using var pen = new Pen(Color.DodgerBlue, 2);
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                
                // Line positions (x coordinates) - same as animation
                int[] lineX = { 3, 7, 11 };
                
                // Static heights for each line (varied for visual interest)
                int[] lineHeights = { 6, 10, 8 }; // Different heights like a frozen moment of animation
                
                for (int i = 0; i < 3; i++)
                {
                    // Calculate line position (centered vertically)
                    int y1 = 8 - lineHeights[i] / 2;
                    int y2 = 8 + lineHeights[i] / 2;
                    
                    // Draw the vertical line
                    g.DrawLine(pen, lineX[i], y1, lineX[i], y2);
                }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void InitializeComponents()
        {
            audioPlayer = new AudioPlayer();
            ttsEngine = new OptimizedPiperEngine();
            clipboardMonitor = new ClipboardMonitor();
            
            clipboardMonitor.ClipboardChanged += OnClipboardChanged;
        }

        private void InitializeAnimation()
        {
            try
            {
                // Store the original icon
                originalIcon = trayIcon.Icon;

                // Load animation frames
                animationFrames = LoadAnimationFrames();

                // Initialize animation timer
                animationTimer = new System.Windows.Forms.Timer();
                animationTimer.Interval = 120; // 120ms per frame (~8.3 FPS)
                animationTimer.Tick += OnAnimationTick;

                Logger.Info($"Animation initialized with {animationFrames?.Length ?? 0} frames");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize animation", ex);
            }
        }

        private Icon[] LoadAnimationFrames()
        {
            var frames = new List<Icon>();
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Try to load animation frames (icon_frame1.ico, icon_frame2.ico, etc.)
            for (int i = 1; i <= 4; i++)
            {
                string framePath = Path.Combine(baseDirectory, $"icon_frame{i}.ico");
                if (File.Exists(framePath))
                {
                    try
                    {
                        frames.Add(new Icon(framePath));
                        Logger.Info($"Loaded animation frame: {framePath}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to load animation frame {framePath}: {ex.Message}");
                    }
                }
            }

            // If no animation frames found, create fallback frames using the original icon
            if (frames.Count == 0)
            {
                Logger.Info("No animation frame files found, creating fallback animation");
                frames.AddRange(CreateFallbackAnimationFrames());
            }

            return frames.ToArray();
        }

        private Icon[] CreateFallbackAnimationFrames()
        {
            // Create "3 vertical lines moving up and down" animation
            var frames = new List<Icon>();
            
            try
            {
                // Create 8 frames for much smoother animation
                int frameCount = 8;
                for (int frame = 0; frame < frameCount; frame++)
                {
                    using var bitmap = new Bitmap(16, 16);
                    using var g = Graphics.FromImage(bitmap);
                    
                    // Clear background
                    g.Clear(Color.Transparent);
                    
                    // Enable anti-aliasing for smoother lines
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    
                    // Define line positions and properties
                    int lineWidth = 2;
                    int baseHeight = 6;
                    int maxHeight = 10;
                    int minHeight = 4;
                    
                    // Line positions (x coordinates)
                    int[] lineX = { 3, 7, 11 };
                    
                    // Create animated heights for each line based on frame
                    // Each line has a different phase in the wave animation
                    using var pen = new Pen(Color.DodgerBlue, lineWidth);
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    
                    for (int line = 0; line < 3; line++)
                    {
                        // Calculate smooth wave animation for each line
                        // More frames and refined phase offsets for smoother motion
                        double frameProgress = (double)frame / frameCount;
                        double phase = (frameProgress + line * 0.33) * Math.PI * 2; // Full sine wave cycle
                        double waveValue = Math.Sin(phase);
                        
                        // Smooth height interpolation with easing
                        double heightRange = (maxHeight - minHeight) / 2.0;
                        double centerHeight = minHeight + heightRange;
                        int lineHeight = (int)(centerHeight + waveValue * heightRange);
                        
                        // Ensure minimum and maximum bounds
                        lineHeight = Math.Max(minHeight, Math.Min(maxHeight, lineHeight));
                        
                        // Calculate line position (centered vertically)
                        int y1 = 8 - lineHeight / 2;
                        int y2 = 8 + lineHeight / 2;
                        
                        // Draw the vertical line
                        g.DrawLine(pen, lineX[line], y1, lineX[line], y2);
                    }
                    
                    frames.Add(Icon.FromHandle(bitmap.GetHicon()));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create fallback animation frames", ex);
                // Return single frame as last resort
                if (originalIcon != null)
                {
                    frames.Add(originalIcon);
                }
            }

            return frames.ToArray();
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            if (animationFrames != null && animationFrames.Length > 0)
            {
                currentFrame = (currentFrame + 1) % animationFrames.Length;
                trayIcon.Icon = animationFrames[currentFrame];
            }
        }

        private void StartTrayIconAnimation()
        {
            if (!settings.AnimateTrayIcon || animationTimer == null || animationFrames == null || animationFrames.Length <= 1)
            {
                return;
            }

            try
            {
                currentFrame = 0;
                animationTimer.Start();
                Logger.Info("Tray icon animation started");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to start tray icon animation", ex);
            }
        }

        private void StopTrayIconAnimation()
        {
            try
            {
                animationTimer?.Stop();
                
                // Reset to original icon
                if (originalIcon != null)
                {
                    trayIcon.Icon = originalIcon;
                }
                
                currentFrame = 0;
                Logger.Info("Tray icon animation stopped");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to stop tray icon animation", ex);
            }
        }

        private void InitializeHotkeys()
        {
            try
            {
                hotkeyManager = new GlobalHotkeyManager();
                RegisterEnabledHotkeys();
                Logger.Info("Global hotkey manager initialized");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize global hotkey manager", ex);
            }
        }

        private void RegisterEnabledHotkeys()
        {
            if (hotkeyManager == null || !settings.EnableHotkeys)
            {
                return;
            }

            // Unregister all existing hotkeys first
            hotkeyManager.UnregisterAllHotkeys();

            // Register each enabled hotkey
            if (settings.MonitoringHotkeyEnabled)
            {
                hotkeyManager.RegisterHotkey(settings.MonitoringHotkey, () => OnToggleMonitoring(null, EventArgs.Empty));
            }

            if (settings.StopSpeechHotkeyEnabled)
            {
                hotkeyManager.RegisterHotkey(settings.StopSpeechHotkey, () => OnStopSpeech(null, EventArgs.Empty));
            }

            if (settings.PauseResumeHotkeyEnabled)
            {
                hotkeyManager.RegisterHotkey(settings.PauseResumeHotkey, () => OnPauseResume(null, EventArgs.Empty));
            }

            if (settings.PresetHotkeyEnabled)
            {
                hotkeyManager.RegisterHotkey(settings.PresetHotkey, () => CyclePreset());
            }

            if (settings.SkipForwardHotkeyEnabled)
            {
                hotkeyManager.RegisterHotkey(settings.SkipForwardHotkey, () => OnSkipForward(null, EventArgs.Empty));
            }

            if (settings.SkipBackHotkeyEnabled)
            {
                hotkeyManager.RegisterHotkey(settings.SkipBackHotkey, () => OnSkipBack(null, EventArgs.Empty));
            }
        }

        private void CyclePreset()
        {
            // Cycle between enabled presets
            var enabledPresets = GetEnabledPresets();
            if (enabledPresets.Count <= 1)
            {
                Logger.Info("Cannot cycle presets: only one or no active presets");
                return;
            }
            
            // Find current preset index among enabled presets
            var currentPresetData = settings.VoicePresets[settings.CurrentActivePreset];
            var currentIndexInEnabled = enabledPresets.IndexOf(currentPresetData);
            
            // Move to next enabled preset
            var nextIndexInEnabled = (currentIndexInEnabled + 1) % enabledPresets.Count;
            var nextPreset = enabledPresets[nextIndexInEnabled];
            var nextPresetIndex = Array.IndexOf(settings.VoicePresets, nextPreset);
            
            SwitchToPreset(nextPresetIndex);
            Logger.Info($"Cycled to preset: {nextPreset.Name}");
        }

        private List<VoicePresetData> GetEnabledPresets()
        {
            return settings.VoicePresets.Where(p => p.Enabled).ToList();
        }

        private void SwitchToPreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= settings.VoicePresets.Length)
            {
                Logger.Warn($"Invalid preset index: {presetIndex}");
                return;
            }

            var preset = settings.VoicePresets[presetIndex];
            if (!preset.Enabled)
            {
                Logger.Warn($"Attempted to switch to disabled preset: {preset.Name}");
                return;
            }

            // Apply preset settings
            settings.DefaultVoice = preset.Model;
            settings.DefaultSpeed = preset.Speed;
            currentSpeed = preset.Speed;
            settings.CurrentActivePreset = presetIndex;
            
            // Save settings
            settings.Save();
            
            // Update menu selections to reflect the change
            UpdateSpeedMenuSelection();
            UpdateVoiceMenuSelection();
            UpdatePresetMenuSelection();
            
            Logger.Info($"Switched to preset: {preset.Name} (Model: {preset.Model}, Speaker: {preset.SpeakerId}, Speed: {preset.Speed})");
        }

        private async void StartServices()
        {
            try
            {
                Logger.Info("Starting TTS engine initialization...");
                await ttsEngine.InitializeAsync();
                Logger.Info("TTS engine initialized successfully");
                
                Logger.Info("Starting clipboard monitor...");
                
                // Capture initial clipboard content to prevent reading it at startup
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        lastReadClipboardText = Clipboard.GetText();
                        Logger.Info("Captured initial clipboard content to ignore at startup");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"Could not capture initial clipboard content: {ex.Message}");
                }
                
                clipboardMonitor.Start();
                Logger.Info("Clipboard monitor started");
                
                // TTS engine ready - no balloon notification
                Logger.Info("=== PiperTray Application Ready ===");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize TTS engine", ex);
                MessageBox.Show($"Failed to initialize TTS engine: {ex.Message}\n\nCheck system.log for details.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void OnClipboardChanged(object? sender, string clipboardText)
        {
            Logger.Info($"Clipboard changed - text length: {clipboardText?.Length ?? 0}");
            
            // Check if monitoring is disabled or speech is paused
            if (!isMonitoringEnabled)
            {
                Logger.Info("Clipboard monitoring is disabled, ignoring change");
                return;
            }
            
            if (isSpeechPaused)
            {
                Logger.Info("Speech is paused, ignoring clipboard change");
                return;
            }
            
            // Check if clipboard text is the same as the last read text
            if (string.Equals(clipboardText, lastReadClipboardText, StringComparison.Ordinal))
            {
                Logger.Info("Clipboard text unchanged from last read, ignoring");
                return;
            }
            
            await ProcessClipboardText(clipboardText);
        }

        private async Task ProcessClipboardText(string? clipboardText)
        {
            if (string.IsNullOrWhiteSpace(clipboardText) || clipboardText.Length > 10000)
            {
                Logger.Info("Ignoring clipboard text (empty or too long)");
                return;
            }

            // Apply dictionary processing to the text
            var processedText = ApplyDictionaryProcessing(clipboardText);
            if (string.IsNullOrWhiteSpace(processedText))
            {
                Logger.Info("Text was filtered out by dictionary rules");
                return;
            }

            // Cancel any ongoing speech generation/playback
            StopCurrentSpeech();

            try
            {
                Logger.Info($"Processing clipboard text: '{clipboardText.Substring(0, Math.Min(50, clipboardText.Length))}...'");
                
                // Create new cancellation token for this speech
                currentSpeechCancellation = new CancellationTokenSource();
                var cancellationToken = currentSpeechCancellation.Token;
                
                // Get voice model and speaker from current preset or default settings
                var voiceModel = settings.DefaultVoice;
                var speakerId = 0;
                
                // If we have an active preset, use its settings
                if (settings.CurrentActivePreset >= 0 && settings.CurrentActivePreset < settings.VoicePresets.Length)
                {
                    var currentPreset = settings.VoicePresets[settings.CurrentActivePreset];
                    if (currentPreset.Enabled)
                    {
                        voiceModel = currentPreset.Model;
                        speakerId = currentPreset.SpeakerId;
                    }
                }
                
                // Use optimized TTS with dynamic voice model and speaker
                var audioData = await ttsEngine.GenerateSpeechAsync(processedText, currentSpeed, voiceModel, speakerId);
                
                // Check if cancelled before starting playback
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.Info("Speech generation cancelled before playback");
                    return;
                }
                
                Logger.Info($"Generated {audioData.Length} bytes of audio, starting playback...");
                
                // Start tray icon animation
                StartTrayIconAnimation();
                
                await audioPlayer.PlayAsync(audioData, cancellationToken);
                Logger.Info("Audio playback completed");
                
                // Stop tray icon animation
                StopTrayIconAnimation();
                
                // Update last read clipboard text to prevent re-reading the same content
                lastReadClipboardText = clipboardText;
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Speech generation/playback was cancelled");
                StopTrayIconAnimation();
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing clipboard text", ex);
                StopTrayIconAnimation();
                // TTS error - no balloon notification, use logger only
            }
            finally
            {
                // Ensure animation is stopped in all cases
                StopTrayIconAnimation();
            }
        }

        private string ApplyDictionaryProcessing(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Check if the text is a URL - if so, ignore it entirely
            var trimmedText = text.Trim();
            if (trimmedText.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                trimmedText.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info("Text is a URL, ignoring entirely");
                return string.Empty;
            }

            var processedText = text;

            // Apply replacements first
            foreach (var replacement in settings.Replacements)
            {
                if (!string.IsNullOrWhiteSpace(replacement.Key) && !string.IsNullOrWhiteSpace(replacement.Value))
                {
                    processedText = processedText.Replace(replacement.Key, replacement.Value, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Remove ignored words
            foreach (var ignoredWord in settings.IgnoredWords)
            {
                if (!string.IsNullOrWhiteSpace(ignoredWord))
                {
                    // Use word boundaries to avoid partial word matches
                    var pattern = $@"\b{Regex.Escape(ignoredWord)}\b";
                    processedText = Regex.Replace(processedText, pattern, "", RegexOptions.IgnoreCase);
                }
            }

            // Clean up extra whitespace
            processedText = Regex.Replace(processedText, @"\s+", " ").Trim();

            return processedText;
        }

        private void OnStopSpeech(object? sender, EventArgs e)
        {
            Logger.Info("Stop Speech requested from tray menu");
            StopCurrentSpeech();
            // Speech stopped - no balloon notification
        }

        private void StopCurrentSpeech()
        {
            // Cancel ongoing speech generation
            currentSpeechCancellation?.Cancel();
            currentSpeechCancellation?.Dispose();
            currentSpeechCancellation = null;
            
            // Stop audio playback
            audioPlayer.Stop();
            
            // Reset pause state since we're stopping everything
            isSpeechPaused = false;
            UpdatePauseMenuText();
            
            Logger.Debug("Current speech stopped");
        }

        private void OnToggleMonitoring(object? sender, EventArgs e)
        {
            isMonitoringEnabled = !isMonitoringEnabled;
            
            if (isMonitoringEnabled)
            {
                clipboardMonitor.Start();
                Logger.Info("Clipboard monitoring enabled");
                // Monitoring enabled - no balloon notification
            }
            else
            {
                clipboardMonitor.Stop();
                Logger.Info("Clipboard monitoring disabled");
                // Monitoring disabled - no balloon notification
            }
            
            UpdateMonitoringMenuText();
        }
        
        private void OnPauseResume(object? sender, EventArgs e)
        {
            if (isSpeechPaused)
            {
                // Resume speech from where it was paused
                audioPlayer.Resume();
                isSpeechPaused = false;
                Logger.Info("Speech resumed from pause point");
                // Speech resumed - no balloon notification
            }
            else
            {
                // Pause speech at current point
                if (audioPlayer.IsPlaying)
                {
                    audioPlayer.Pause();
                    isSpeechPaused = true;
                    Logger.Info("Speech paused at current point");
                }
                else
                {
                    Logger.Info("No speech currently playing to pause");
                }
                // Speech paused - no balloon notification
            }
            
            UpdatePauseMenuText();
        }

        private void OnSkipForward(object? sender, EventArgs e)
        {
            audioPlayer.SkipForward(settings.SkipIntervalSeconds);
            Logger.Info($"Skip forward {settings.SkipIntervalSeconds} seconds requested");
        }

        private void OnSkipBack(object? sender, EventArgs e)
        {
            audioPlayer.SkipBack(settings.SkipIntervalSeconds);
            Logger.Info($"Skip back {settings.SkipIntervalSeconds} seconds requested");
        }
        
        private void SetVoice(string voiceName)
        {
            settings.DefaultVoice = voiceName;
            settings.Save();
            
            Logger.Info($"Voice changed to {voiceName}");
            // Voice changed - no balloon notification
            
            UpdateVoiceMenuSelection();
        }
        
        private async void OnExportToWav(object? sender, EventArgs e)
        {
            try
            {
                // Get clipboard text
                string clipboardText = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(clipboardText))
                {
                    MessageBox.Show("Clipboard is empty or contains no text.", "Export to WAV", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                if (clipboardText.Length > 10000)
                {
                    var result = MessageBox.Show($"Text is {clipboardText.Length} characters long. This may take a while to export. Continue?", 
                        "Export to WAV", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }
                
                // Show save dialog
                using (var saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "WAV files (*.wav)|*.wav";
                    saveDialog.Title = "Export Speech to WAV";
                    saveDialog.FileName = "speech_export.wav";
                    
                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        Logger.Info($"Exporting clipboard text to WAV: {saveDialog.FileName}");
                        // Generating audio for export - no balloon notification
                        
                        // Get voice model and speaker from current preset or default settings
                        var voiceModel = settings.DefaultVoice;
                        var speakerId = 0;
                        
                        // If we have an active preset, use its settings
                        if (settings.CurrentActivePreset >= 0 && settings.CurrentActivePreset < settings.VoicePresets.Length)
                        {
                            var currentPreset = settings.VoicePresets[settings.CurrentActivePreset];
                            if (currentPreset.Enabled)
                            {
                                voiceModel = currentPreset.Model;
                                speakerId = currentPreset.SpeakerId;
                            }
                        }
                        
                        // Generate audio using TTS engine
                        var audioData = await ttsEngine.GenerateSpeechAsync(clipboardText, currentSpeed, voiceModel, speakerId);
                        
                        // Write to WAV file
                        File.WriteAllBytes(saveDialog.FileName, audioData);
                        
                        Logger.Info($"Successfully exported {audioData.Length} bytes to {saveDialog.FileName}");
                        // Audio exported - no balloon notification
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error exporting to WAV", ex);
                MessageBox.Show($"Failed to export audio: {ex.Message}\n\nCheck system.log for details.", "Export Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetSpeed(int speed)
        {
            currentSpeed = speed;
            settings.DefaultSpeed = speed;
            settings.Save(); // Save settings when speed changes
            
            Logger.Info($"Speech speed set to {speed}");
            // Speed changed - no balloon notification
            
            // Update menu items to show current selection
            UpdateSpeedMenuSelection();
        }
        
        private void UpdateMonitoringMenuText()
        {
            var monitoringItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text.Contains("Monitoring"));
            if (monitoringItem != null)
            {
                monitoringItem.Checked = isMonitoringEnabled;
                monitoringItem.Text = "Monitoring";
            }
        }
        
        private void UpdatePauseMenuText()
        {
            var pauseItem = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text.Contains("Pause") || item.Text.Contains("Resume"));
            if (pauseItem != null)
            {
                pauseItem.Text = isSpeechPaused ? "Resume" : "Pause";
            }
        }
        
        private void UpdateVoiceMenuSelection()
        {
            var voiceMenu = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Voice");
            if (voiceMenu != null)
            {
                foreach (ToolStripMenuItem item in voiceMenu.DropDownItems)
                {
                    item.Checked = false;
                }
                
                // Mark current voice as checked by matching the display name
                var currentVoiceDisplayName = VoiceModelDetector.GetDisplayName(settings.DefaultVoice);
                var selectedItem = voiceMenu.DropDownItems.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Text == currentVoiceDisplayName);
                if (selectedItem != null)
                {
                    selectedItem.Checked = true;
                }
            }
        }

        private void UpdatePresetMenuSelection()
        {
            var presetsMenu = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Presets");
            if (presetsMenu != null)
            {
                foreach (ToolStripMenuItem item in presetsMenu.DropDownItems)
                {
                    item.Checked = false;
                }
                
                // Mark current preset as checked
                if (settings.CurrentActivePreset >= 0 && settings.CurrentActivePreset < settings.VoicePresets.Length)
                {
                    var currentPreset = settings.VoicePresets[settings.CurrentActivePreset];
                    var selectedItem = presetsMenu.DropDownItems.OfType<ToolStripMenuItem>()
                        .FirstOrDefault(item => item.Text == currentPreset.Name);
                    if (selectedItem != null)
                    {
                        selectedItem.Checked = true;
                    }
                }
            }
        }

        private void UpdateSpeedMenuSelection()
        {
            var speedMenu = contextMenu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Speed");
            if (speedMenu != null)
            {
                foreach (ToolStripMenuItem item in speedMenu.DropDownItems)
                {
                    item.Checked = false;
                }
                
                // Mark current speed as checked
                var currentSpeedText = currentSpeed switch
                {
                    1 => "1 (Very Slow)",
                    2 => "2",
                    3 => "3",
                    4 => "4",
                    5 => "5 (Normal)",
                    6 => "6",
                    7 => "7",
                    8 => "8 (Fast)",
                    9 => "9",
                    10 => "10 (Very Fast)",
                    _ => "5 (Normal)"
                };
                
                var selectedItem = speedMenu.DropDownItems.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(item => item.Text == currentSpeedText);
                if (selectedItem != null)
                {
                    selectedItem.Checked = true;
                }
            }
        }

        private void OnSettings(object? sender, EventArgs e)
        {
            try
            {
                Logger.Info("Opening settings window");
                using (var settingsForm = new SettingsForm())
                {
                    var result = settingsForm.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        // Reload settings and refresh menu
                        settings = AppSettings.Load();
                        RefreshTrayMenu();
                        
                        Logger.Info("Settings saved successfully");
                        // Settings saved - no balloon notification
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error opening settings window", ex);
                MessageBox.Show($"Error opening settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void RefreshTrayMenu()
        {
            // Dispose of current context menu
            contextMenu?.Dispose();
            
            // Recreate the context menu
            CreateContextMenu();
            
            // Update the tray icon's context menu
            if (trayIcon != null)
            {
                trayIcon.ContextMenuStrip = contextMenu;
            }
            
            // Refresh hotkeys when settings change
            RegisterEnabledHotkeys();
            
            // Update menu states
            UpdateSpeedMenuSelection();
            UpdateVoiceMenuSelection();
            UpdatePresetMenuSelection();
            UpdateMonitoringMenuText();
            UpdatePauseMenuText();
        }

        private void OnExit(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            StopCurrentSpeech();
            
            // Stop and cleanup animation
            StopTrayIconAnimation();
            animationTimer?.Dispose();
            
            // Dispose animation frames
            if (animationFrames != null)
            {
                foreach (var frame in animationFrames)
                {
                    frame?.Dispose();
                }
            }
            
            clipboardMonitor?.Stop();
            hotkeyManager?.Dispose();
            ttsEngine?.Dispose();
            audioPlayer?.Dispose();
            Application.Exit();
        }
    }
}