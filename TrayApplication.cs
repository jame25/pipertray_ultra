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
            
            // Check if voice models are available
            CheckVoiceModelsAvailability();
            
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
                
                // Check if language switching is enabled and process accordingly
                var audioData = await GenerateSpeechWithLanguageSwitching(processedText, cancellationToken);
                
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
                        
                        // Apply dictionary processing
                        var processedText = ApplyDictionaryProcessing(clipboardText);
                        
                        // Generate audio using language switching if configured
                        var audioData = await GenerateSpeechWithLanguageSwitching(processedText, CancellationToken.None);
                        
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

        private void CheckVoiceModelsAvailability()
        {
            try
            {
                var voiceModelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper-executable");
                
                // Check if piper-executable directory exists
                if (!Directory.Exists(voiceModelsPath))
                {
                    MessageBox.Show("Please add voice models to the 'piper-executable' directory.", 
                        "No Voice Models Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Check for actual .onnx model files
                var onnxFiles = Directory.GetFiles(voiceModelsPath, "*.onnx", SearchOption.TopDirectoryOnly);
                var actualVoiceModels = onnxFiles.Where(file => 
                {
                    var fileName = Path.GetFileNameWithoutExtension(file).ToLower();
                    // Exclude non-voice models
                    return !fileName.Contains("silero_vad") && 
                           !fileName.Contains("test_voice") && 
                           !fileName.Contains("_vad") && 
                           (fileName.Contains("-") || fileName.Contains("_"));
                }).ToArray();
                
                if (actualVoiceModels.Length == 0)
                {
                    MessageBox.Show("Please add voice models to the 'piper-executable' directory.", 
                        "No Voice Models Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error checking voice models availability", ex);
            }
        }

        private async Task<byte[]> GenerateSpeechWithLanguageSwitching(string text, CancellationToken cancellationToken)
        {
            // Check if any language switching pairs are enabled
            var enabledPairs = settings.LanguageSwitching.LanguageVoicePairs
                .Where(pair => pair.Enabled && !string.IsNullOrEmpty(pair.Language) && !string.IsNullOrEmpty(pair.VoiceModel))
                .ToArray();

            if (enabledPairs.Length == 0)
            {
                // No language switching configured, use default voice
                return await GenerateWithDefaultVoice(text);
            }

            // Detect languages in the text and split into segments
            var segments = DetectLanguageSegments(text, enabledPairs);
            
            Logger.Info($"Detected {segments.Count} language segments:");
            for (int i = 0; i < segments.Count; i++)
            {
                Logger.Info($"  Segment {i + 1}: Language='{segments[i].DetectedLanguage}', Text='{segments[i].Text.Substring(0, Math.Min(50, segments[i].Text.Length))}...' (length: {segments[i].Text.Length})");
            }
            
            if (segments.Count == 1 && string.IsNullOrEmpty(segments[0].DetectedLanguage))
            {
                // No specific language detected, use default voice
                return await GenerateWithDefaultVoice(text);
            }

            // Generate audio for each segment with appropriate voice
            var audioChunks = new List<byte[]>();
            
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                Logger.Info($"Processing segment {i + 1}/{segments.Count}: '{segment.Text.Substring(0, Math.Min(30, segment.Text.Length))}...'");
                
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.Info("Cancellation requested, stopping segment processing");
                    break;
                }

                byte[] segmentAudio;
                
                if (!string.IsNullOrEmpty(segment.DetectedLanguage))
                {
                    // Find the voice model for this language
                    var languagePair = enabledPairs.FirstOrDefault(p => p.Language == segment.DetectedLanguage);
                    if (languagePair != null)
                    {
                        Logger.Info($"Using voice model '{languagePair.VoiceModel}' with speed {languagePair.Speed} for language '{segment.DetectedLanguage}'");
                        segmentAudio = await ttsEngine.GenerateSpeechAsync(segment.Text, languagePair.Speed, languagePair.VoiceModel, 0);
                    }
                    else
                    {
                        Logger.Info($"Language '{segment.DetectedLanguage}' detected but no voice configured, using default");
                        segmentAudio = await GenerateWithDefaultVoice(segment.Text);
                    }
                }
                else
                {
                    Logger.Info("No language detected for this segment, using default voice");
                    segmentAudio = await GenerateWithDefaultVoice(segment.Text);
                }
                
                Logger.Info($"Generated {segmentAudio.Length} bytes of audio for segment {i + 1}");
                audioChunks.Add(segmentAudio);
            }

            // Combine all audio chunks using proper WAV combination
            return CombineWavFiles(audioChunks.ToArray());
        }

        private async Task<byte[]> GenerateWithDefaultVoice(string text)
        {
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
            
            return await ttsEngine.GenerateSpeechAsync(text, currentSpeed, voiceModel, speakerId);
        }

        private List<LanguageSegment> DetectLanguageSegments(string text, LanguageVoicePair[] enabledPairs)
        {
            var segments = new List<LanguageSegment>();
            var sentences = SplitIntoSentences(text);
            
            foreach (var sentence in sentences)
            {
                var detectedLanguage = DetectLanguage(sentence, enabledPairs);
                segments.Add(new LanguageSegment { Text = sentence, DetectedLanguage = detectedLanguage });
            }
            
            // Merge consecutive segments with the same language
            var mergedSegments = new List<LanguageSegment>();
            foreach (var segment in segments)
            {
                if (mergedSegments.Count > 0 && 
                    mergedSegments.Last().DetectedLanguage == segment.DetectedLanguage)
                {
                    // Merge with previous segment
                    mergedSegments.Last().Text += " " + segment.Text;
                }
                else
                {
                    mergedSegments.Add(segment);
                }
            }
            
            return mergedSegments;
        }

        private string[] SplitIntoSentences(string text)
        {
            // Enhanced sentence splitting that preserves language segments better
            var sentences = new List<string>();
            
            // First, split by paragraph breaks which often indicate language changes
            var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var paragraph in paragraphs)
            {
                // Then split each paragraph by sentence endings
                var sentenceEnders = new[] { '.', '!', '?', '\n', '\r' };
                var currentSentence = "";
                
                foreach (char c in paragraph)
                {
                    currentSentence += c;
                    
                    if (sentenceEnders.Contains(c))
                    {
                        var trimmed = currentSentence.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && trimmed.Length > 3) // Minimum sentence length
                        {
                            sentences.Add(trimmed);
                        }
                        currentSentence = "";
                    }
                }
                
                // Add remaining text as last sentence
                var remaining = currentSentence.Trim();
                if (!string.IsNullOrEmpty(remaining) && remaining.Length > 3)
                {
                    sentences.Add(remaining);
                }
            }
            
            // If no sentences were found, return the whole text as one sentence
            if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(text))
            {
                sentences.Add(text.Trim());
            }
            
            return sentences.ToArray();
        }

        private string DetectLanguage(string text, LanguageVoicePair[] enabledPairs)
        {
            // Simple language detection based on character patterns
            // This is a basic implementation - could be enhanced with more sophisticated algorithms
            Logger.Info($"Detecting language for text: '{text.Substring(0, Math.Min(50, text.Length))}...'");
            Logger.Info($"Enabled language pairs: {string.Join(", ", enabledPairs.Select(p => p.Language))}");
            
            // Try to find the best matching language by scoring each one
            var languageScores = new Dictionary<string, double>();
            
            foreach (var pair in enabledPairs)
            {
                double score = GetLanguageScore(text, pair.Language);
                languageScores[pair.Language] = score;
                Logger.Info($"Language '{pair.Language}' score: {score:F2}");
            }
            
            // Find the language with the highest score
            var bestMatch = languageScores.Where(kvp => kvp.Value > 0).OrderByDescending(kvp => kvp.Value).FirstOrDefault();
            
            if (bestMatch.Key != null && bestMatch.Value > 0.05) // Minimum threshold (lowered for better detection)
            {
                Logger.Info($"Detected language: {bestMatch.Key} (score: {bestMatch.Value:F2})");
                return bestMatch.Key;
            }
            
            // If no language scored well, check if English is configured and text is Latin script
            if (enabledPairs.Any(p => p.Language == "en"))
            {
                bool isLatinScript = text.All(c => c <= 127 || char.IsWhiteSpace(c) || char.IsPunctuation(c));
                if (isLatinScript)
                {
                    Logger.Info("No specific language detected, but text is Latin script and English is configured - using English");
                    return "en";
                }
            }
            
            Logger.Info("No specific language detected, using default voice");
            return ""; // No specific language detected
        }

        private double GetLanguageScore(string text, string language)
        {
            var lowerText = text.ToLower();
            double score = 0.0;
            
            // Character-based scoring
            double charScore = GetCharacterScore(text, language);
            
            // Word-based scoring
            double wordScore = GetWordScore(lowerText, language);
            
            // English fallback: if text appears to be Latin script and no other language scores well
            if (language == "en")
            {
                // Check if text is primarily Latin characters (likely English if no special chars)
                bool isLatinScript = text.All(c => c <= 127 || char.IsWhiteSpace(c) || char.IsPunctuation(c));
                if (isLatinScript && charScore == 0.0)
                {
                    // Give English a baseline score for Latin-only text
                    charScore = 0.1;
                }
            }
            
            // Combine scores with weights
            score = (charScore * 0.4) + (wordScore * 0.6);
            
            return score;
        }

        private double GetCharacterScore(string text, string language)
        {
            int totalChars = text.Length;
            if (totalChars == 0) return 0.0;
            
            int matchingChars = 0;
            
            switch (language.ToLower())
            {
                case "ar": // Arabic
                    matchingChars = text.Count(c => c >= '\u0600' && c <= '\u06FF');
                    break;
                case "zh": // Chinese
                    matchingChars = text.Count(c => c >= '\u4E00' && c <= '\u9FFF');
                    break;
                case "ru": // Russian
                    matchingChars = text.Count(c => c >= '\u0400' && c <= '\u04FF');
                    break;
                case "el": // Greek
                    matchingChars = text.Count(c => c >= '\u0370' && c <= '\u03FF');
                    break;
                case "ka": // Georgian
                    matchingChars = text.Count(c => c >= '\u10A0' && c <= '\u10FF');
                    break;
                case "sl": // Slovenian
                    matchingChars = text.Count(c => "čćšžđ".Contains(char.ToLower(c)));
                    break;
                case "de": // German
                    matchingChars = text.Count(c => "äöüß".Contains(char.ToLower(c)));
                    break;
                case "fr": // French
                    matchingChars = text.Count(c => "àâäéèêëïîôöùûüÿç".Contains(char.ToLower(c)));
                    break;
                case "es": // Spanish
                    matchingChars = text.Count(c => "ñü".Contains(char.ToLower(c)));
                    break;
                // Add more character-based detection for other languages as needed
                default:
                    return 0.0; // No specific character patterns
            }
            
            return (double)matchingChars / totalChars;
        }

        private double GetWordScore(string lowerText, string language)
        {
            string[] commonWords = GetCommonWordsForLanguage(language);
            if (commonWords.Length == 0) return 0.0;
            
            var words = lowerText.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 0.0;
            
            int matches = words.Count(word => commonWords.Contains(word));
            return (double)matches / words.Length;
        }

        private string[] GetCommonWordsForLanguage(string language)
        {
            switch (language.ToLower())
            {
                case "en":
                    return new[] { "the", "be", "to", "of", "and", "a", "in", "that", "have", "i", "it", "for", "not", "on", "with", "he", "as", "you", "do", "at", "this", "but", "his", "by", "from", "they", "we", "say", "her", "she", "or", "an", "will", "my", "one", "all", "would", "there", "their", "what", "so", "up", "out", "if", "about", "who", "get", "which", "go", "me", "when", "make", "can", "like", "time", "no", "just", "him", "know", "take", "people", "into", "year", "your", "good", "some", "could", "them", "see", "other", "than", "then", "now", "look", "only", "come", "its", "over", "think", "also", "back", "after", "use", "two", "how", "our", "work", "first", "well", "way", "even", "new", "want", "because", "any", "these", "give", "day", "most", "us", "is", "was", "are", "been", "has", "had", "were", "said", "each", "which", "she", "do", "how", "their", "if", "will", "up", "other", "about", "out", "many", "then", "them", "these", "so", "some", "her", "would", "make", "like", "into", "him", "time", "has", "two", "more", "go", "no", "way", "could", "my", "than", "first", "water", "been", "call", "who", "its", "now", "find", "long", "down", "day", "did", "get", "come", "made", "may", "part" };
                case "sl":
                    return new[] { "in", "da", "se", "je", "na", "za", "z", "v", "ne", "to", "so", "po", "od", "ali", "če", "ki", "ter", "ima", "do", "kot", "bo", "bi", "vse", "lahko", "samo", "še", "bilo", "tudi", "ker", "pri" };
                case "de":
                    return new[] { "der", "die", "und", "in", "den", "von", "zu", "das", "mit", "sich", "des", "auf", "für", "ist", "im", "dem", "nicht", "ein", "eine", "als" };
                case "fr":
                    return new[] { "le", "de", "et", "à", "un", "il", "être", "et", "en", "avoir", "que", "pour", "dans", "ce", "son", "une", "sur", "avec", "ne", "se" };
                case "es":
                    return new[] { "el", "la", "de", "que", "y", "en", "un", "es", "se", "no", "te", "lo", "le", "da", "su", "por", "son", "con", "para" };
                case "it":
                    return new[] { "il", "di", "che", "e", "la", "il", "un", "a", "per", "non", "in", "una", "si", "me", "mi", "ha", "lo", "li", "le", "gli" };
                case "ru":
                    return new[] { "и", "в", "не", "на", "я", "быть", "он", "с", "что", "а", "по", "это", "она", "этот", "к", "но", "они", "мы", "как", "из", "у", "который", "то", "за", "свой", "ее", "так", "от", "же", "для", "или", "при", "один", "все", "очень", "если", "где" };
                // Add more languages as needed
                default:
                    return new string[0];
            }
        }


        private byte[] CombineWavFiles(byte[][] wavFiles)
        {
            if (wavFiles == null || wavFiles.Length == 0)
                return new byte[0];
                
            if (wavFiles.Length == 1)
                return wavFiles[0];

            Logger.Info($"Combining {wavFiles.Length} WAV files for mixed language audio");

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
                    Logger.Info($"Extracted {pcmData.Length} bytes of PCM data from WAV segment");
                }
            }

            Logger.Info($"Combined total PCM data: {combinedPcmData.Count} bytes");

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

            Logger.Info($"Created combined WAV file: {memoryStream.Length} bytes total");

            return memoryStream.ToArray();
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

    public class LanguageSegment
    {
        public string Text { get; set; } = "";
        public string DetectedLanguage { get; set; } = "";
    }
}
