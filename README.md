<p align="center"> <img width="256" height="256" src="https://github.com/user-attachments/assets/b8a6dffe-b31e-4068-a96c-9490ca65de96"></p>

# PiperTray Ultra

Ultra-fast Text-to-Speech tray application using [Piper TTS](https://github.com/rhasspy/piper) with advanced features, pronunciation dictionaries, animated visual feedback, and smart clipboard management. Voice models are available [here](https://huggingface.co/rhasspy/piper-voices/tree/main).

## Features

### 🚀 **Ultra-Fast Speech Generation**
- **Custom Piper.exe with daemon mode** - Maintains model in memory for instant response
- **Optimized ONNX runtime** - Enhanced parallelism and memory arena allocation
- **Tensor pooling** - Reusable memory buffers for improved performance
- **Asynchronous phonemization** - Producer-consumer pipeline for reduced latency
- **Intelligent text chunking** - Advanced sentence boundary detection with optimal chunk sizing
- **Phoneme caching with LRU** - Cache common word phonemizations to reduce eSpeak overhead
- **Sentence-level audio callbacks** - Streaming output for lower latency on multi-sentence texts

### 📋 **Smart Clipboard Management**
- **Real-time clipboard monitoring** - Instantly converts copied text to speech
- **Intelligent startup behavior** - Ignores existing clipboard content at launch, only reads new changes
- **Duplicate detection** - Prevents re-reading identical clipboard content
- **URL filtering** - Automatically ignores HTTP/HTTPS URLs to prevent reading web addresses
- **Monitoring toggle** - Enable/disable clipboard monitoring on demand
- **True pause/resume functionality** - Pause speech at exact point and resume from same position
- **Smart text filtering** - Ignores empty or overly long clipboard content (>10,000 chars)
- **Cancellable speech** - Stop current speech when new text is copied

### 🎵 **Advanced Audio Features**
- **High-quality neural TTS** - Natural intonation with multiple voice options
- **Optimized speech speed** - 10 speed levels with proper length_scale mapping (1.5 to 0.5)
- **Voice selection** - Choose between voices
- **Voice presets** - 4 customizable presets with speed 5 default for balanced performance
- **Language switching** - Automatic language detection and voice switching for multilingual content
- **Professional audio navigation**:
  - Skip forward/back with configurable intervals (5-60 seconds, default 10s)
  - Precise seeking using NAudio's native capabilities
  - Position tracking with current time and total duration
  - Boundary protection (won't skip past beginning or end)
- **WAV export** - Export clipboard text directly to WAV files
- **Streaming playback** - Audio starts playing while remaining chunks are processed
- **Cancellation support** - Stop speech playback instantly

### 🖥️ **Rich System Tray Interface**
- **Lightweight system tray app** - Runs quietly in the background
- **Animated visual feedback** - Smooth three vertical lines animation during speech (enabled by default)
- **Smart preset menu** - Presets menu only appears when presets are actually enabled
- **Customizable context menu**:
  - Stop Speech - Cancel current TTS playback
  - Pause/Resume - True pause/resume at exact playback position
  - Skip - Professional audio navigation (forward/back by configurable intervals)
  - Monitoring - Enable/disable clipboard monitoring (with visual indicator)
  - Speed (1-10) - Adjustable speech rate with descriptive labels
  - Voice - Select between available voice models
  - Export to WAV - Save clipboard text as audio file
  - Settings - Comprehensive configuration options
  - Exit - Clean shutdown
- **Menu customization** - Show/hide individual menu items via settings (including Skip controls)
- **Silent operation** - No intrusive balloon notifications, all events logged to system.log
- **Custom icon support** - Uses icon.ico file for both executable and tray icon

### 🌍 **Intelligent Language Switching**
- **Automatic language detection** - Smart recognition of 35+ languages including Arabic, Chinese, Russian, and European languages
- **Seamless voice switching** - Configure specific voice models for each language with individual speed settings
- **Mixed-language support** - Automatically switches voices within the same text (e.g., English-French articles)
- **Advanced text segmentation** - Intelligent sentence and paragraph-level language detection
- **Scoring algorithm** - Character-based and word-frequency analysis for accurate language identification
- **Proper audio combination** - Seamlessly combines audio from different voice models into single playback
- **Supported languages**: ar, ca, cs, cy, da, de, el, en, es, fa, fi, fr, hu, is, it, ka, kk, lb, lv, ne, nl, no, pl, pt, ro, ru, sk, sl, sr, sv, sw, tr, uk, vi, zh
- **UTF-8 encoding support** - Perfect handling of Cyrillic, Arabic, Chinese, and other non-Latin scripts

### 📝 **Pronunciation Dictionaries**
- **Ignored Words** - Skip specific words during speech (e.g., filler words like "um", "uh")
- **Text Replacements** - Substitute words/phrases for better pronunciation (e.g., "LHC" → "Large Hadron Collider")
- **Real-time processing** - All dictionary rules applied automatically during speech generation
- **Persistent storage** - Dictionary settings saved with application configuration
- **Easy management** - Add/remove words through intuitive Settings interface

### ⚙️ **Comprehensive Settings System**
- **Multi-tab settings window** with five organized sections:
  - **Appearance Tab**: 
    - Show/Hide Menu Items - Customize which tray menu options are visible (Monitoring, Stop Speech, Pause/Resume, Skip, Speed, Voice, Export to WAV)
    - Audio Visualization - Animated tray icon during speech (enabled by default)
  - **Hotkeys Tab**: 
    - Global hotkey support for 6 key actions (Monitoring, Stop Speech, Pause/Resume, Voice, Skip Forward, Skip Back)
    - Customizable key combinations with intuitive defaults
    - Skip interval configuration (5-60 seconds)
  - **Presets Tab**: 
    - 4 voice presets with individual enable/disable controls
    - Speed 5 default for optimal balance of quality and speed
    - Model, speaker, and speed configuration per preset
- **Language Switching Tab**:
    - 6 configurable language/voice pairs for automatic language detection
    - 35+ supported languages (Arabic, Chinese, Russian, European languages, etc.)
    - Individual voice model and speed settings per language
    - Seamless multilingual text processing with automatic voice switching
  - **Dictionaries Tab**:
    - Ignored Words management with add/remove functionality
    - Text Replacements with "From → To" format display
    - Real-time preview of dictionary entries
  - **Advanced Tab**:
    - Phoneme cache settings (size and enable/disable)
    - Intelligent text chunking configuration
- **JSON-based persistence** - Settings saved to application directory
- **Real-time application** - Changes apply immediately without restart

### 🔧 **Technical Excellence**
- **Daemon architecture** - Single model load, multiple requests with JSON protocol
- **Performance optimizations**:
  - ONNX runtime parallelism and memory arena allocation
  - Tensor pooling for memory efficiency
  - LRU phoneme caching (configurable size, default 10,000 words)
  - Asynchronous phonemization pipeline with producer-consumer pattern
  - Intelligent text chunking with sentence boundary detection
  - Optional batch inference for compatible models
- **Memory optimization** - Efficient audio data handling with minimal allocations
- **Error resilience** - Automatic daemon restart on failures with detailed error reporting
- **Comprehensive logging** - Detailed system.log for troubleshooting and performance monitoring
- **Cross-platform C++ optimizations** - Enhanced Piper TTS engine with speed parameter support

## System Requirements

- Windows with .NET 8.0
- Custom Piper TTS executable with daemon and speed parameter support
- Voice models (https://huggingface.co/rhasspy/piper-voices/tree/main)
- eSpeak-ng data files for phonemization
- NAudio library for high-quality audio playback

## Architecture

### Custom Piper Daemon
The application uses a heavily modified Piper TTS executable with enhanced daemon mode and performance optimizations:

- **Daemon startup**: `piper.exe --model "model.onnx" --espeak_data "path" --daemon`
- **Enhanced request format**: JSON `{"text": "Hello world", "speed": 7}`
- **Response format**: 4-byte size header + raw PCM audio data
- **Persistent model**: Loaded once in memory for sub-second response times
- **Speed parameter support**: Integer values 1-10 (higher = faster) instead of decimal length_scale
- **Performance optimizations**: Tensor pooling, phoneme caching, and intelligent chunking

### Advanced Processing Pipeline
1. **Text input** via clipboard monitoring with pause/resume control
2. **Intelligent text chunking** with sentence boundary detection and optimal sizing
3. **Asynchronous phonemization** with LRU word-level caching
4. **Enhanced daemon requests** with speed control and JSON protocol
5. **Tensor pooling** for efficient memory management
6. **Streaming audio generation** with sentence-level callbacks
7. **PCM to WAV conversion** in memory with immediate NAudio playback

## Configuration

The application automatically locates required files and supports comprehensive customization:

### File Locations
- **Piper executable**: `piper-executable/piper.exe` (custom daemon-enabled build)
- **Voice models**: `piper-executable/`
- **eSpeak data**: `piper-executable/espeak-ng-data/`
- **Settings**: `settings.json` in application directory

### Settings Configuration
- **Appearance**: Menu item visibility (7 customizable options), animated tray icon
- **Audio**: Speed (1-10 with optimized length_scale), voice selection, auto-read clipboard, skip intervals
- **Language Switching**: 6 configurable language/voice pairs with individual speed settings for 35+ languages
- **Dictionaries**: Ignored words, text replacements with intuitive management interface
- **Performance**: Phoneme cache size (default 10,000), intelligent chunking
- **Hotkeys**: Global shortcuts for 6 key actions with configurable skip intervals
- **Presets**: 4 customizable voice presets with speed 5 default, smart visibility (only shown when enabled)

## Usage

1. **Start the application** - PiperTray will appear in the system tray with animated three vertical lines icon
2. **Copy any text** - The application automatically converts it to speech with smart filtering:
   - Only reads new clipboard changes (ignores existing content at startup)
   - Skips duplicate content to prevent re-reading
   - Automatically ignores URLs (http:// and https://)
   - Applies pronunciation dictionaries for better speech quality
3. **Visual feedback** - Animated tray icon provides smooth visual indication during speech
4. **Control playback** via the rich tray icon context menu:
   - Adjust speed (1-10) with optimized length_scale mapping
   - Switch voices (Jane Medium/Amy Low) for variety
   - Use voice presets for quick configuration changes
   - Pause/Resume speech at exact position for interruption control
   - Skip forward/back (configurable 5-60 second intervals) for navigation
   - Toggle monitoring for selective TTS activation
   - Export clipboard text to WAV files
5. **Use keyboard shortcuts** for instant control:
   - Ctrl+Shift+M (Monitoring toggle)
   - Ctrl+Shift+S (Stop Speech)
   - Ctrl+Shift+P (Pause/Resume)
   - Ctrl+Shift+V (Voice switching)
   - Ctrl+Shift+→ (Skip Forward)
   - Ctrl+Shift+← (Skip Back)
6. **Configure language switching** via Settings → Language Switching:
   - Enable up to 6 language/voice pairs for automatic detection
   - Set specific voice models and speeds for each language
   - Perfect for multilingual content (news articles, academic papers, etc.)
7. **Customize pronunciation** via Settings → Dictionaries:
   - Add ignored words to skip filler words or unwanted terms
   - Create text replacements for better pronunciation (acronyms, technical terms)
8. **Customize interface** via Settings → Appearance → Show/Hide Menu Items
9. **Monitor performance** in `system.log` for detailed operation info and cache statistics

## Performance

### Speed Optimizations
- **Model loading**: One-time startup cost reduced to ~1-2 seconds with optimizations
- **Speech generation**: Sub-second response for short text with phoneme caching
- **Large text**: Intelligent chunking with asynchronous processing and streaming output
- **Memory efficiency**: Tensor pooling and LRU caching minimize allocations
- **Cache performance**: Typical 80-90% hit rate on phoneme cache for common text

### Performance Metrics
- **Phoneme cache**: Configurable size (1,000-50,000 words), default 10,000
- **Chunking**: Optimal 150 chars/chunk, min 50, max 300 for balanced processing
- **Real-time factors**: Typically 2-5x faster than real-time playback
- **Memory usage**: Optimized for continuous 24/7 operation

## Audio Quality

- **Sample rate**: 22.05 kHz
- **Bit depth**: 16-bit signed PCM
- **Channels**: Mono
- **Format**: Direct PCM to WAV conversion for optimal quality
- **Voices**: High-quality neural TTS models with natural intonation
- **Speed control**: 10 levels with optimized length_scale mapping (1=1.5 very slow to 10=0.5 very fast) preserving audio quality
