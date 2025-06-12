using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PiperTray
{
    public class AppSettings
    {
        // Appearance settings
        public bool AnimateTrayIcon { get; set; } = true;
        
        // Menu item visibility settings
        public bool ShowMonitoringMenuItem { get; set; } = true;
        public bool ShowStopSpeechMenuItem { get; set; } = true;
        public bool ShowPauseResumeMenuItem { get; set; } = false;
        public bool ShowSkipMenuItem { get; set; } = false;
        public bool ShowSpeedMenuItem { get; set; } = true;
        public bool ShowVoiceMenuItem { get; set; } = true;
        public bool ShowPresetsMenuItem { get; set; } = true;
        public bool ShowExportToWavMenuItem { get; set; } = false;

        // Hotkey settings
        public bool EnableHotkeys { get; set; } = true;
        public string MonitoringHotkey { get; set; } = "Ctrl+Shift+M";
        public string StopSpeechHotkey { get; set; } = "Ctrl+Shift+S";
        public string PauseResumeHotkey { get; set; } = "Ctrl+Shift+P";
        public string PresetHotkey { get; set; } = "Ctrl+Shift+V";
        public string SkipForwardHotkey { get; set; } = "Ctrl+Shift+Right";
        public string SkipBackHotkey { get; set; } = "Ctrl+Shift+Left";
        
        // Individual hotkey enable/disable flags
        public bool MonitoringHotkeyEnabled { get; set; } = false;
        public bool StopSpeechHotkeyEnabled { get; set; } = false;
        public bool PauseResumeHotkeyEnabled { get; set; } = false;
        public bool PresetHotkeyEnabled { get; set; } = false;
        public bool SkipForwardHotkeyEnabled { get; set; } = false;
        public bool SkipBackHotkeyEnabled { get; set; } = false;

        // Skip settings
        public int SkipIntervalSeconds { get; set; } = 10;

        // Voice and TTS settings
        public int DefaultSpeed { get; set; } = 5;
        public string DefaultVoice { get; set; } = "";
        public bool AutoReadClipboard { get; set; } = true;
        public string CurrentPreset { get; set; } = "Default";

        // Preset settings
        public VoicePresetData[] VoicePresets { get; set; } = VoicePresetData.GetDefaultPresets();
        public int CurrentActivePreset { get; set; } = 0; // Index of currently active preset

        // Advanced settings
        public bool EnablePhonemeCache { get; set; } = true;
        public int PhonemeCacheSize { get; set; } = 10000;
        public bool EnableIntelligentChunking { get; set; } = true;
        public bool EnableLogging { get; set; } = false;

        // Dictionary settings
        public string[] IgnoredWords { get; set; } = new string[0];
        public Dictionary<string, string> Replacements { get; set; } = new Dictionary<string, string>();

        // Static methods for loading/saving
        private static readonly string SettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "settings.json"
        );

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    
                    // Ensure we have a valid default voice
                    EnsureValidDefaultVoice(settings);
                    
                    Logger.Info($"Settings loaded from {SettingsPath}");
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading settings", ex);
            }

            Logger.Info("Using default settings");
            var defaultSettings = new AppSettings();
            EnsureValidDefaultVoice(defaultSettings);
            return defaultSettings;
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsPath, json);
                Logger.Info($"Settings saved to {SettingsPath}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving settings", ex);
                throw;
            }
        }

        public static string GetSettingsPath()
        {
            return SettingsPath;
        }

        private static void EnsureValidDefaultVoice(AppSettings settings)
        {
            // If no voice is set or the current voice is not available, set a valid default
            if (string.IsNullOrEmpty(settings.DefaultVoice) || !VoiceModelDetector.IsModelAvailable(settings.DefaultVoice))
            {
                var defaultVoice = VoiceModelDetector.GetDefaultModel();
                settings.DefaultVoice = defaultVoice;
                Logger.Info($"Set default voice to: {defaultVoice}");
            }
        }
    }

    public class VoicePreset
    {
        public string Name { get; set; } = "";
        public int Speed { get; set; } = 5;
        public string Voice { get; set; } = "en_us-jane-medium";
        public bool AutoReadClipboard { get; set; } = true;
        public string Description { get; set; } = "";

        public static VoicePreset[] GetDefaultPresets()
        {
            return new VoicePreset[]
            {
                new VoicePreset
                {
                    Name = "Default (Jane Medium)",
                    Speed = 5,
                    Voice = "en_us-jane-medium",
                    AutoReadClipboard = true,
                    Description = "Balanced speed and quality for general use"
                },
                new VoicePreset
                {
                    Name = "Fast Reading",
                    Speed = 8,
                    Voice = "en_us-jane-medium",
                    AutoReadClipboard = true,
                    Description = "Fast speech for quick content consumption"
                },
                new VoicePreset
                {
                    Name = "Slow and Clear",
                    Speed = 3,
                    Voice = "en_us-jane-medium",
                    AutoReadClipboard = true,
                    Description = "Slow, clear speech for learning and comprehension"
                },
                new VoicePreset
                {
                    Name = "Presentation Mode",
                    Speed = 4,
                    Voice = "en_us-jane-medium",
                    AutoReadClipboard = false,
                    Description = "Professional pace for presentations"
                },
                new VoicePreset
                {
                    Name = "Podcast Style",
                    Speed = 6,
                    Voice = "en_us-jane-medium",
                    AutoReadClipboard = true,
                    Description = "Natural conversational pace"
                }
            };
        }
    }

    public class VoicePresetData
    {
        public string Name { get; set; } = "";
        public string Model { get; set; } = "";
        public int SpeakerId { get; set; } = 0;
        public int Speed { get; set; } = 5;
        public bool Enabled { get; set; } = false;

        public static VoicePresetData[] GetDefaultPresets()
        {
            var defaultModel = VoiceModelDetector.GetDefaultModel();
            return new VoicePresetData[]
            {
                new VoicePresetData
                {
                    Name = "Preset 1",
                    Model = defaultModel,
                    SpeakerId = 0,
                    Speed = 5,
                    Enabled = false
                },
                new VoicePresetData
                {
                    Name = "Preset 2",
                    Model = defaultModel,
                    SpeakerId = 0,
                    Speed = 5,
                    Enabled = false
                },
                new VoicePresetData
                {
                    Name = "Preset 3",
                    Model = defaultModel,
                    SpeakerId = 0,
                    Speed = 5,
                    Enabled = false
                },
                new VoicePresetData
                {
                    Name = "Preset 4",
                    Model = defaultModel,
                    SpeakerId = 0,
                    Speed = 5,
                    Enabled = false
                }
            };
        }
    }
}