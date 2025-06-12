using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PiperTray
{
    public static class VoiceModelDetector
    {
        private static readonly string VoiceModelsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper-executable");
        
        /// <summary>
        /// Gets all available voice models by scanning for .onnx files in the piper-executable directory
        /// </summary>
        /// <returns>List of voice model names (without .onnx extension)</returns>
        public static List<string> GetAvailableVoiceModels()
        {
            var voiceModels = new List<string>();
            
            try
            {
                if (!Directory.Exists(VoiceModelsPath))
                {
                    Logger.Warn($"Voice models directory not found: {VoiceModelsPath}");
                    return GetFallbackModels();
                }
                
                var onnxFiles = Directory.GetFiles(VoiceModelsPath, "*.onnx", SearchOption.TopDirectoryOnly);
                
                foreach (var file in onnxFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    
                    // Skip non-voice model files (like silero_vad.onnx or test files)
                    if (IsVoiceModel(fileName))
                    {
                        voiceModels.Add(fileName);
                    }
                }
                
                // Sort models alphabetically for consistent ordering
                voiceModels.Sort();
                
                Logger.Info($"Detected {voiceModels.Count} voice models: {string.Join(", ", voiceModels)}");
                
                // If no models found, return fallback models
                if (voiceModels.Count == 0)
                {
                    Logger.Warn("No voice models detected, using fallback models");
                    return GetFallbackModels();
                }
                
                return voiceModels;
            }
            catch (Exception ex)
            {
                Logger.Error("Error detecting voice models", ex);
                return GetFallbackModels();
            }
        }
        
        /// <summary>
        /// Checks if a filename represents a voice model (not a utility model like VAD)
        /// </summary>
        /// <param name="fileName">The filename without extension</param>
        /// <returns>True if it's a voice model</returns>
        private static bool IsVoiceModel(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
                
            // Skip known non-voice models
            var skipPatterns = new[] { "silero_vad", "test_voice", "vad", "test" };
            
            foreach (var pattern in skipPatterns)
            {
                if (fileName.ToLower().Contains(pattern))
                    return false;
            }
            
            // Voice models typically follow patterns like:
            // en_us-jane-medium, en_US-amy-low, etc.
            // They usually contain language codes and voice names
            return fileName.Contains("-") || fileName.Contains("_");
        }
        
        /// <summary>
        /// Returns fallback voice models if detection fails
        /// </summary>
        /// <returns>List of fallback voice model names</returns>
        private static List<string> GetFallbackModels()
        {
            return new List<string>
            {
                "en_us-jane-medium",
                "en_US-amy-low"
            };
        }
        
        /// <summary>
        /// Gets a user-friendly display name for a voice model
        /// </summary>
        /// <param name="modelName">The technical model name</param>
        /// <returns>A user-friendly display name</returns>
        public static string GetDisplayName(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return "Unknown";
                
            // Convert technical names to more readable format
            return modelName switch
            {
                "en_us-jane-medium" => "Jane (Medium)",
                "en_US-amy-low" => "Amy (Low)",
                "en_us-jane-low" => "Jane (Low)",
                "en_US-amy-medium" => "Amy (Medium)",
                "en_US-l2arctic-medium" => "L2arctic (Medium)",
                "en_US-l2arctic-low" => "L2arctic (Low)",
                "en_US-l2arctic-high" => "L2arctic (High)",
                _ => FormatDisplayName(modelName)
            };
        }
        
        /// <summary>
        /// Formats a technical model name into a more readable display name
        /// </summary>
        /// <param name="modelName">The technical model name</param>
        /// <returns>A formatted display name</returns>
        private static string FormatDisplayName(string modelName)
        {
            try
            {
                // Handle patterns like "en_us-jane-medium" or "es_es-marta-low"
                var parts = modelName.Split('-');
                if (parts.Length >= 2)
                {
                    var voice = char.ToUpper(parts[1][0]) + parts[1].Substring(1);
                    var quality = parts.Length > 2 ? $"({char.ToUpper(parts[2][0]) + parts[2].Substring(1)})" : "";
                    
                    return $"{voice} {quality}".Trim();
                }
                
                // Fallback: just capitalize and clean up
                return modelName.Replace("_", " ").Replace("-", " ");
            }
            catch
            {
                return modelName; // Return original if formatting fails
            }
        }
        
        /// <summary>
        /// Checks if a specific voice model is available
        /// </summary>
        /// <param name="modelName">The model name to check</param>
        /// <returns>True if the model is available</returns>
        public static bool IsModelAvailable(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return false;
                
            var availableModels = GetAvailableVoiceModels();
            return availableModels.Contains(modelName);
        }
        
        /// <summary>
        /// Gets the first available voice model, useful for defaults
        /// </summary>
        /// <returns>The first available voice model name</returns>
        public static string GetDefaultModel()
        {
            var models = GetAvailableVoiceModels();
            return models.Count > 0 ? models[0] : "en_us-jane-medium";
        }

        /// <summary>
        /// Gets the available speakers for a specific voice model
        /// </summary>
        /// <param name="modelName">The voice model name</param>
        /// <returns>List of available speaker IDs and names</returns>
        public static List<SpeakerInfo> GetAvailableSpeakers(string modelName)
        {
            var speakers = new List<SpeakerInfo>();
            
            if (string.IsNullOrEmpty(modelName))
            {
                return GetDefaultSpeakers();
            }
            
            try
            {
                var jsonPath = Path.Combine(VoiceModelsPath, $"{modelName}.onnx.json");
                
                if (!File.Exists(jsonPath))
                {
                    Logger.Warn($"JSON file not found for model {modelName}: {jsonPath}");
                    return GetDefaultSpeakers();
                }
                
                var jsonContent = File.ReadAllText(jsonPath);
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;
                
                // Get number of speakers
                var numSpeakers = 1;
                if (root.TryGetProperty("num_speakers", out var numSpeakersElement))
                {
                    numSpeakers = numSpeakersElement.GetInt32();
                }
                
                // Get speaker ID map if available
                var speakerIdMap = new Dictionary<string, int>();
                if (root.TryGetProperty("speaker_id_map", out var speakerMapElement) && speakerMapElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in speakerMapElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Number)
                        {
                            speakerIdMap[property.Name] = property.Value.GetInt32();
                        }
                    }
                }
                
                // If we have named speakers, use those
                if (speakerIdMap.Count > 0)
                {
                    foreach (var kvp in speakerIdMap.OrderBy(x => x.Value))
                    {
                        speakers.Add(new SpeakerInfo(kvp.Value, kvp.Key));
                    }
                }
                else
                {
                    // Generate numbered speakers based on num_speakers
                    for (int i = 0; i < numSpeakers; i++)
                    {
                        var speakerName = numSpeakers > 1 ? $"Speaker {i}" : "Default";
                        speakers.Add(new SpeakerInfo(i, speakerName));
                    }
                }
                
                Logger.Info($"Model {modelName} has {speakers.Count} speakers");
                return speakers;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading speaker info for model {modelName}", ex);
                return GetDefaultSpeakers();
            }
        }
        
        /// <summary>
        /// Gets default speaker list for fallback scenarios
        /// </summary>
        /// <returns>Default speaker list</returns>
        private static List<SpeakerInfo> GetDefaultSpeakers()
        {
            return new List<SpeakerInfo> { new SpeakerInfo(0, "Default") };
        }
        
        /// <summary>
        /// Gets the maximum speaker ID for a model (useful for validation)
        /// </summary>
        /// <param name="modelName">The voice model name</param>
        /// <returns>Maximum speaker ID</returns>
        public static int GetMaxSpeakerId(string modelName)
        {
            var speakers = GetAvailableSpeakers(modelName);
            return speakers.Count > 0 ? speakers.Max(s => s.Id) : 0;
        }
        
        /// <summary>
        /// Validates if a speaker ID is valid for a given model
        /// </summary>
        /// <param name="modelName">The voice model name</param>
        /// <param name="speakerId">The speaker ID to validate</param>
        /// <returns>True if the speaker ID is valid</returns>
        public static bool IsValidSpeakerId(string modelName, int speakerId)
        {
            var speakers = GetAvailableSpeakers(modelName);
            return speakers.Any(s => s.Id == speakerId);
        }
    }
    
    /// <summary>
    /// Represents information about a speaker in a voice model
    /// </summary>
    public class SpeakerInfo
    {
        public int Id { get; }
        public string Name { get; }
        public string DisplayName => Id.ToString(); // Show only the number
        
        public SpeakerInfo(int id, string name)
        {
            Id = id;
            Name = name ?? $"Speaker {id}";
        }
        
        public override string ToString()
        {
            return DisplayName;
        }
    }
}