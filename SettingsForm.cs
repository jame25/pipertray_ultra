using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PiperTray
{
    public partial class SettingsForm : Form
    {
        private TabControl tabControl;
        private TabPage appearanceTab;
        private TabPage hotkeysTab;
        private TabPage presetsTab;
        private TabPage dictionariesTab;
        private TabPage advancedTab;
        private Button okButton;
        private Button cancelButton;
        private Button applyButton;
        private AppSettings settings;

        // UI Controls that need to be accessed for settings binding
        private CheckBox animateTrayIconCheck;
        private ComboBox speedComboBox;
        private ComboBox voiceComboBox;
        private CheckBox autoReadCheck;
        private CheckBox enableCacheCheck;
        private NumericUpDown cacheSizeNumeric;
        private CheckBox batchingCheck;
        private CheckBox enableLoggingCheck;
        
        // Menu item visibility controls
        private CheckBox showMonitoringCheck;
        private CheckBox showStopSpeechCheck;
        private CheckBox showPauseResumeCheck;
        private CheckBox showSkipCheck;
        private CheckBox showSpeedCheck;
        private CheckBox showVoiceCheck;
        private CheckBox showPresetsCheck;
        private CheckBox showExportToWavCheck;
        private NumericUpDown skipIntervalNumeric;
        
        // Preset controls
        private TextBox[] presetNameTextBoxes = new TextBox[4];
        private ComboBox[] presetModelCombos = new ComboBox[4];
        private ComboBox[] presetSpeakerCombos = new ComboBox[4];
        private ComboBox[] presetSpeedCombos = new ComboBox[4];
        private Button[] presetNumberButtons = new Button[4];
        private bool[] presetEnabled = new bool[4]; // Track which presets are enabled

        // Dictionary controls
        private ListBox ignoredWordsListBox;
        private TextBox ignoredWordTextBox;
        private Button addIgnoredWordButton;
        private Button removeIgnoredWordButton;
        private ListBox replacementsListBox;
        private TextBox replacementFromTextBox;
        private TextBox replacementToTextBox;
        private Button addReplacementButton;
        private Button removeReplacementButton;
        private Dictionary<string, string> displayNameToModelMap = new Dictionary<string, string>(); // Map display names to model names

        // Hotkey controls
        private ComboBox monitoringModifierCombo;
        private TextBox monitoringKeyText;
        private CheckBox monitoringEnabledCheck;
        private ComboBox stopSpeechModifierCombo;
        private TextBox stopSpeechKeyText;
        private CheckBox stopSpeechEnabledCheck;
        private ComboBox pauseResumeModifierCombo;
        private TextBox pauseResumeKeyText;
        private CheckBox pauseResumeEnabledCheck;
        private ComboBox presetModifierCombo;
        private TextBox presetKeyText;
        private CheckBox presetEnabledCheck;
        private ComboBox skipForwardModifierCombo;
        private TextBox skipForwardKeyText;
        private CheckBox skipForwardEnabledCheck;
        private ComboBox skipBackModifierCombo;
        private TextBox skipBackKeyText;
        private CheckBox skipBackEnabledCheck;

        public SettingsForm()
        {
            // Load current settings
            settings = AppSettings.Load();
            
            InitializeComponent();
            SetupTabs();
            SetupButtons();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "PiperTray Ultra Settings";
            this.Size = new Size(500, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
        }

        private void SetupTabs()
        {
            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(10)
            };

            // Appearance Tab
            appearanceTab = new TabPage("Appearance");
            SetupAppearanceTab();

            // Hotkeys Tab
            hotkeysTab = new TabPage("Hotkeys");
            SetupHotkeysTab();

            // Presets Tab
            presetsTab = new TabPage("Presets");
            SetupPresetsTab();

            // Dictionaries Tab
            dictionariesTab = new TabPage("Dictionaries");
            SetupDictionariesTab();

            // Advanced Tab
            advancedTab = new TabPage("Advanced");
            SetupAdvancedTab();

            tabControl.TabPages.Add(appearanceTab);
            tabControl.TabPages.Add(hotkeysTab);
            tabControl.TabPages.Add(presetsTab);
            tabControl.TabPages.Add(dictionariesTab);
            tabControl.TabPages.Add(advancedTab);

            this.Controls.Add(tabControl);
        }

        private void SetupAppearanceTab()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Show/Hide Menu Items section
            var menuItemsGroup = new GroupBox
            {
                Text = "Show/Hide Menu Items",
                Size = new Size(450, 160),
                Location = new Point(10, 10)
            };

            showMonitoringCheck = new CheckBox
            {
                Text = "Monitoring",
                Location = new Point(15, 25),
                Size = new Size(120, 20),
                Checked = true
            };

            showStopSpeechCheck = new CheckBox
            {
                Text = "Stop Speech",
                Location = new Point(15, 50),
                Size = new Size(120, 20),
                Checked = true
            };

            showPauseResumeCheck = new CheckBox
            {
                Text = "Pause | Resume",
                Location = new Point(15, 75),
                Size = new Size(120, 20),
                Checked = false
            };

            showSkipCheck = new CheckBox
            {
                Text = "Skip",
                Location = new Point(15, 100),
                Size = new Size(80, 20),
                Checked = false
            };

            showSpeedCheck = new CheckBox
            {
                Text = "Speed",
                Location = new Point(150, 25),
                Size = new Size(80, 20),
                Checked = true
            };

            showVoiceCheck = new CheckBox
            {
                Text = "Voice",
                Location = new Point(150, 50),
                Size = new Size(80, 20),
                Checked = true
            };

            showPresetsCheck = new CheckBox
            {
                Text = "Presets",
                Location = new Point(150, 75),
                Size = new Size(80, 20),
                Checked = true
            };

            showExportToWavCheck = new CheckBox
            {
                Text = "Export to WAV",
                Location = new Point(150, 100),
                Size = new Size(120, 20),
                Checked = false
            };

            menuItemsGroup.Controls.AddRange(new Control[] {
                showMonitoringCheck, showStopSpeechCheck, showPauseResumeCheck, showSkipCheck,
                showSpeedCheck, showVoiceCheck, showPresetsCheck, showExportToWavCheck
            });

            // Audio visualization
            var audioGroup = new GroupBox
            {
                Text = "Audio Visualization",
                Size = new Size(450, 55),
                Location = new Point(10, 180)
            };

            animateTrayIconCheck = new CheckBox
            {
                Text = "Animate tray icon during speech",
                Location = new Point(15, 25),
                Size = new Size(250, 20)
            };

            audioGroup.Controls.Add(animateTrayIconCheck);

            panel.Controls.Add(menuItemsGroup);
            panel.Controls.Add(audioGroup);
            appearanceTab.Controls.Add(panel);
        }

        private void SetupHotkeysTab()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Global hotkeys
            var globalGroup = new GroupBox
            {
                Text = "Global Hotkeys",
                Size = new Size(450, 210),
                Location = new Point(10, 10)
            };

            // Monitoring row
            var monitoringLabel = new Label
            {
                Text = "Monitoring:",
                Location = new Point(15, 25),
                Size = new Size(100, 23)
            };

            monitoringModifierCombo = new ComboBox
            {
                Location = new Point(120, 25),
                Size = new Size(96, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            monitoringModifierCombo.Items.AddRange(new string[] { "ALT", "CTRL", "SHIFT", "CTRL+SHIFT", "ALT+SHIFT", "CTRL+ALT" });
            monitoringModifierCombo.SelectedItem = "ALT";

            monitoringKeyText = new TextBox
            {
                Location = new Point(226, 25),
                Size = new Size(80, 23),
                Text = "None"
            };

            monitoringEnabledCheck = new CheckBox
            {
                Location = new Point(320, 27),
                Size = new Size(20, 20),
                Checked = false
            };
            monitoringEnabledCheck.CheckedChanged += OnHotkeyEnabledChanged;

            // Stop Speech row
            var stopSpeechLabel = new Label
            {
                Text = "Stop Speech:",
                Location = new Point(15, 55),
                Size = new Size(100, 23)
            };

            stopSpeechModifierCombo = new ComboBox
            {
                Location = new Point(120, 55),
                Size = new Size(96, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            stopSpeechModifierCombo.Items.AddRange(new string[] { "ALT", "CTRL", "SHIFT", "CTRL+SHIFT", "ALT+SHIFT", "CTRL+ALT" });
            stopSpeechModifierCombo.SelectedItem = "ALT";

            stopSpeechKeyText = new TextBox
            {
                Location = new Point(226, 55),
                Size = new Size(80, 23),
                Text = "None"
            };

            stopSpeechEnabledCheck = new CheckBox
            {
                Location = new Point(320, 57),
                Size = new Size(20, 20),
                Checked = false
            };
            stopSpeechEnabledCheck.CheckedChanged += OnHotkeyEnabledChanged;

            // Pause | Resume row
            var pauseResumeLabel = new Label
            {
                Text = "Pause | Resume:",
                Location = new Point(15, 85),
                Size = new Size(100, 23)
            };

            pauseResumeModifierCombo = new ComboBox
            {
                Location = new Point(120, 85),
                Size = new Size(96, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            pauseResumeModifierCombo.Items.AddRange(new string[] { "ALT", "CTRL", "SHIFT", "CTRL+SHIFT", "ALT+SHIFT", "CTRL+ALT" });
            pauseResumeModifierCombo.SelectedItem = "ALT";

            pauseResumeKeyText = new TextBox
            {
                Location = new Point(226, 85),
                Size = new Size(80, 23),
                Text = "None"
            };

            pauseResumeEnabledCheck = new CheckBox
            {
                Location = new Point(320, 87),
                Size = new Size(20, 20),
                Checked = false
            };
            pauseResumeEnabledCheck.CheckedChanged += OnHotkeyEnabledChanged;

            // Preset row
            var presetLabel = new Label
            {
                Text = "Preset:",
                Location = new Point(15, 115),
                Size = new Size(100, 23)
            };

            presetModifierCombo = new ComboBox
            {
                Location = new Point(120, 115),
                Size = new Size(96, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            presetModifierCombo.Items.AddRange(new string[] { "ALT", "CTRL", "SHIFT", "CTRL+SHIFT", "ALT+SHIFT", "CTRL+ALT" });
            presetModifierCombo.SelectedItem = "ALT";

            presetKeyText = new TextBox
            {
                Location = new Point(226, 115),
                Size = new Size(80, 23),
                Text = "None"
            };

            presetEnabledCheck = new CheckBox
            {
                Location = new Point(320, 117),
                Size = new Size(20, 20),
                Checked = false
            };
            presetEnabledCheck.CheckedChanged += OnHotkeyEnabledChanged;

            // Skip Forward row
            var skipForwardLabel = new Label
            {
                Text = "Skip Forward:",
                Location = new Point(15, 145),
                Size = new Size(100, 23)
            };

            skipForwardModifierCombo = new ComboBox
            {
                Location = new Point(120, 145),
                Size = new Size(96, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            skipForwardModifierCombo.Items.AddRange(new string[] { "ALT", "CTRL", "SHIFT", "CTRL+SHIFT", "ALT+SHIFT", "CTRL+ALT" });
            skipForwardModifierCombo.SelectedItem = "ALT";

            skipForwardKeyText = new TextBox
            {
                Location = new Point(226, 145),
                Size = new Size(80, 23),
                Text = "None"
            };

            skipForwardEnabledCheck = new CheckBox
            {
                Location = new Point(320, 147),
                Size = new Size(20, 20),
                Checked = false
            };
            skipForwardEnabledCheck.CheckedChanged += OnHotkeyEnabledChanged;

            // Skip Back row
            var skipBackLabel = new Label
            {
                Text = "Skip Back:",
                Location = new Point(15, 175),
                Size = new Size(100, 23)
            };

            skipBackModifierCombo = new ComboBox
            {
                Location = new Point(120, 175),
                Size = new Size(96, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            skipBackModifierCombo.Items.AddRange(new string[] { "ALT", "CTRL", "SHIFT", "CTRL+SHIFT", "ALT+SHIFT", "CTRL+ALT" });
            skipBackModifierCombo.SelectedItem = "ALT";

            skipBackKeyText = new TextBox
            {
                Location = new Point(226, 175),
                Size = new Size(80, 23),
                Text = "None"
            };

            skipBackEnabledCheck = new CheckBox
            {
                Location = new Point(320, 177),
                Size = new Size(20, 20),
                Checked = false
            };
            skipBackEnabledCheck.CheckedChanged += OnHotkeyEnabledChanged;

            globalGroup.Controls.AddRange(new Control[] {
                monitoringLabel, monitoringModifierCombo, monitoringKeyText, monitoringEnabledCheck,
                stopSpeechLabel, stopSpeechModifierCombo, stopSpeechKeyText, stopSpeechEnabledCheck,
                pauseResumeLabel, pauseResumeModifierCombo, pauseResumeKeyText, pauseResumeEnabledCheck,
                presetLabel, presetModifierCombo, presetKeyText, presetEnabledCheck,
                skipForwardLabel, skipForwardModifierCombo, skipForwardKeyText, skipForwardEnabledCheck,
                skipBackLabel, skipBackModifierCombo, skipBackKeyText, skipBackEnabledCheck
            });

            // Skip interval section
            var skipGroup = new GroupBox
            {
                Text = "Skip Settings",
                Size = new Size(450, 60),
                Location = new Point(10, 230)
            };

            var skipIntervalLabel = new Label
            {
                Text = "Skip interval:",
                Location = new Point(15, 25),
                Size = new Size(80, 20)
            };

            skipIntervalNumeric = new NumericUpDown
            {
                Location = new Point(100, 23),
                Size = new Size(60, 23),
                Minimum = 5,
                Maximum = 60,
                Value = 10,
                Increment = 5
            };

            var secondsLabel = new Label
            {
                Text = "seconds",
                Location = new Point(170, 25),
                Size = new Size(60, 20)
            };

            skipGroup.Controls.AddRange(new Control[] {
                skipIntervalLabel, skipIntervalNumeric, secondsLabel
            });

            panel.Controls.Add(globalGroup);
            panel.Controls.Add(skipGroup);
            hotkeysTab.Controls.Add(panel);
        }

        private void SetupPresetsTab()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Create column headers
            var nameHeader = new Label
            {
                Text = "Name",
                Location = new Point(10, 10),
                Size = new Size(80, 20)
            };

            var modelHeader = new Label
            {
                Text = "Model",
                Location = new Point(100, 10),
                Size = new Size(132, 20)
            };

            var speakerHeader = new Label
            {
                Text = "Speaker",
                Location = new Point(242, 10),
                Size = new Size(60, 20)
            };

            var speedHeader = new Label
            {
                Text = "Speed",
                Location = new Point(312, 10),
                Size = new Size(50, 20)
            };

            panel.Controls.AddRange(new Control[] {
                nameHeader, modelHeader, speakerHeader, speedHeader
            });

            // Create 4 preset rows
            for (int i = 0; i < 4; i++)
            {
                int yPos = 40 + (i * 30);

                // Name textbox
                presetNameTextBoxes[i] = new TextBox
                {
                    Text = $"Preset {i + 1}",
                    Location = new Point(10, yPos),
                    Size = new Size(80, 23),
                    Enabled = false, // Disabled by default
                    BackColor = SystemColors.Control,
                    ForeColor = SystemColors.GrayText
                };

                // Model combobox (increased width by 10%: 120 * 1.1 = 132)
                presetModelCombos[i] = new ComboBox
                {
                    Location = new Point(100, yPos),
                    Size = new Size(132, 23),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Enabled = false, // Disabled by default
                    BackColor = SystemColors.Control,
                    ForeColor = SystemColors.GrayText,
                    Tag = i // Store the preset index for event handling
                };
                // Get available voice models and add them to the dropdown
                var availableModels = VoiceModelDetector.GetAvailableVoiceModels();
                foreach (var model in availableModels)
                {
                    var displayName = VoiceModelDetector.GetDisplayName(model);
                    presetModelCombos[i].Items.Add(displayName);
                    
                    // Store mapping for later retrieval
                    if (!displayNameToModelMap.ContainsKey(displayName))
                    {
                        displayNameToModelMap[displayName] = model;
                    }
                }
                
                // Set default selection - first model for first preset, second for others (if available)
                if (presetModelCombos[i].Items.Count > 0)
                {
                    if (i == 0 || presetModelCombos[i].Items.Count == 1)
                    {
                        presetModelCombos[i].SelectedIndex = 0;
                    }
                    else
                    {
                        presetModelCombos[i].SelectedIndex = Math.Min(1, presetModelCombos[i].Items.Count - 1);
                    }
                }
                
                // Add event handler for model selection changes
                presetModelCombos[i].SelectedIndexChanged += PresetModelCombo_SelectedIndexChanged;

                // Speaker combobox (adjusted position for wider Model combobox)
                presetSpeakerCombos[i] = new ComboBox
                {
                    Location = new Point(242, yPos),
                    Size = new Size(60, 23),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Enabled = false, // Disabled by default
                    BackColor = SystemColors.Control,
                    ForeColor = SystemColors.GrayText
                };
                
                // Initialize speaker dropdown based on the selected model
                UpdateSpeakerDropdown(i);

                // Speed combobox (adjusted position)
                presetSpeedCombos[i] = new ComboBox
                {
                    Location = new Point(312, yPos),
                    Size = new Size(50, 23),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Enabled = false, // Disabled by default
                    BackColor = SystemColors.Control,
                    ForeColor = SystemColors.GrayText
                };
                presetSpeedCombos[i].Items.AddRange(new string[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" });
                presetSpeedCombos[i].SelectedIndex = i == 0 ? 4 : 0; // First preset speed 5 (index 4), others speed 1 (index 0)

                // Initialize as disabled
                presetEnabled[i] = false;

                panel.Controls.AddRange(new Control[] {
                    presetNameTextBoxes[i], presetModelCombos[i], presetSpeakerCombos[i], presetSpeedCombos[i]
                });
            }

            // Create numbered buttons (1, 2, 3, 4) for enabling/disabling presets
            for (int i = 0; i < 4; i++)
            {
                presetNumberButtons[i] = new Button
                {
                    Text = (i + 1).ToString(),
                    Location = new Point(10 + (i * 35), 170),
                    Size = new Size(30, 25),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = SystemColors.Control,
                    ForeColor = SystemColors.ControlText,
                    Tag = i, // Store the preset index
                    TabStop = false // Prevent the button from receiving tab focus
                };
                presetNumberButtons[i].Click += NumberButton_Click;
                panel.Controls.Add(presetNumberButtons[i]);
            }

            presetsTab.Controls.Add(panel);
        }

        private void SetupAdvancedTab()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Advanced settings
            var advancedGroup = new GroupBox
            {
                Text = "Advanced Settings",
                Size = new Size(450, 150),
                Location = new Point(10, 10)
            };

            var cacheLabel = new Label
            {
                Text = "Phoneme Cache:",
                Location = new Point(15, 25),
                Size = new Size(100, 20)
            };

            enableCacheCheck = new CheckBox
            {
                Text = "Enable phoneme caching",
                Location = new Point(120, 25),
                Checked = true
            };

            var cacheSizeLabel = new Label
            {
                Text = "Cache Size:",
                Location = new Point(15, 55),
                Size = new Size(80, 20)
            };

            cacheSizeNumeric = new NumericUpDown
            {
                Location = new Point(120, 53),
                Size = new Size(80, 23),
                Minimum = 1000,
                Maximum = 50000,
                Value = 10000,
                Increment = 1000
            };

            var wordsLabel = new Label
            {
                Text = "words",
                Location = new Point(210, 55),
                Size = new Size(50, 20)
            };

            var clearCacheButton = new Button
            {
                Text = "Clear Cache",
                Location = new Point(270, 53),
                Size = new Size(80, 25)
            };

            batchingCheck = new CheckBox
            {
                Text = "Enable intelligent text chunking",
                Location = new Point(15, 85),
                Size = new Size(250, 20),
                Checked = true
            };

            enableLoggingCheck = new CheckBox
            {
                Text = "Enable logging",
                Location = new Point(15, 115),
                Size = new Size(250, 20),
                Checked = false
            };

            advancedGroup.Controls.AddRange(new Control[] {
                cacheLabel, enableCacheCheck,
                cacheSizeLabel, cacheSizeNumeric, wordsLabel, clearCacheButton,
                batchingCheck, enableLoggingCheck
            });

            panel.Controls.Add(advancedGroup);
            advancedTab.Controls.Add(panel);
        }

        private void SetupDictionariesTab()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // Ignored Words Group
            var ignoredWordsGroup = new GroupBox
            {
                Text = "Ignored Words",
                Size = new Size(450, 135),
                Location = new Point(10, 10)
            };

            ignoredWordsListBox = new ListBox
            {
                Location = new Point(15, 25),
                Size = new Size(200, 80)
            };

            ignoredWordTextBox = new TextBox
            {
                Location = new Point(230, 25),
                Size = new Size(120, 23),
                PlaceholderText = "Enter word to ignore"
            };

            addIgnoredWordButton = new Button
            {
                Text = "Add",
                Location = new Point(360, 25),
                Size = new Size(60, 23)
            };
            addIgnoredWordButton.Click += AddIgnoredWord_Click;

            removeIgnoredWordButton = new Button
            {
                Text = "Remove",
                Location = new Point(360, 55),
                Size = new Size(60, 23)
            };
            removeIgnoredWordButton.Click += RemoveIgnoredWord_Click;

            ignoredWordsGroup.Controls.AddRange(new Control[] {
                ignoredWordsListBox, ignoredWordTextBox, addIgnoredWordButton, removeIgnoredWordButton
            });

            // Replacements Group - moved up to fill the space
            var replacementsGroup = new GroupBox
            {
                Text = "Replacements",
                Size = new Size(450, 180),
                Location = new Point(10, 155)
            };

            replacementsListBox = new ListBox
            {
                Location = new Point(15, 25),
                Size = new Size(200, 98)
            };

            var fromLabel = new Label
            {
                Text = "From:",
                Location = new Point(230, 25),
                Size = new Size(40, 20)
            };

            replacementFromTextBox = new TextBox
            {
                Location = new Point(230, 45),
                Size = new Size(120, 23),
                PlaceholderText = "Original word"
            };

            var toLabel = new Label
            {
                Text = "To:",
                Location = new Point(230, 75),
                Size = new Size(40, 20)
            };

            replacementToTextBox = new TextBox
            {
                Location = new Point(230, 95),
                Size = new Size(120, 23),
                PlaceholderText = "Replacement"
            };

            addReplacementButton = new Button
            {
                Text = "Add",
                Location = new Point(360, 45),
                Size = new Size(60, 23)
            };
            addReplacementButton.Click += AddReplacement_Click;

            removeReplacementButton = new Button
            {
                Text = "Remove",
                Location = new Point(360, 75),
                Size = new Size(60, 23)
            };
            removeReplacementButton.Click += RemoveReplacement_Click;

            replacementsGroup.Controls.AddRange(new Control[] {
                replacementsListBox, fromLabel, replacementFromTextBox, toLabel, 
                replacementToTextBox, addReplacementButton, removeReplacementButton
            });

            panel.Controls.AddRange(new Control[] {
                ignoredWordsGroup, replacementsGroup
            });

            dictionariesTab.Controls.Add(panel);
        }

        private void SetupButtons()
        {
            var buttonPanel = new Panel
            {
                Height = 50,
                Dock = DockStyle.Bottom,
                Padding = new Padding(10)
            };

            okButton = new Button
            {
                Text = "OK",
                Size = new Size(75, 25),
                Location = new Point(315, 12),
                DialogResult = DialogResult.OK
            };
            okButton.Click += OkButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(75, 25),
                Location = new Point(400, 12),
                DialogResult = DialogResult.Cancel
            };

            applyButton = new Button
            {
                Text = "Apply",
                Size = new Size(75, 25),
                Location = new Point(230, 12)
            };
            applyButton.Click += ApplyButton_Click;

            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(cancelButton);
            buttonPanel.Controls.Add(applyButton);

            this.Controls.Add(buttonPanel);
            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            ApplySettings();
            this.Close();
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            ApplySettings();
        }

        private void LoadSettings()
        {
            // Menu visibility settings
            showMonitoringCheck.Checked = settings.ShowMonitoringMenuItem;
            showStopSpeechCheck.Checked = settings.ShowStopSpeechMenuItem;
            showPauseResumeCheck.Checked = settings.ShowPauseResumeMenuItem;
            showSkipCheck.Checked = settings.ShowSkipMenuItem;
            showSpeedCheck.Checked = settings.ShowSpeedMenuItem;
            showVoiceCheck.Checked = settings.ShowVoiceMenuItem;
            showPresetsCheck.Checked = settings.ShowPresetsMenuItem;
            showExportToWavCheck.Checked = settings.ShowExportToWavMenuItem;
            
            // Appearance settings
            animateTrayIconCheck.Checked = settings.AnimateTrayIcon;

            // Skip settings
            skipIntervalNumeric.Value = settings.SkipIntervalSeconds;
            
            // Load hotkey settings
            LoadHotkeySettings();

            // Voice settings (legacy - these controls are no longer in the new Presets tab design)
            if (speedComboBox != null)
            {
                speedComboBox.SelectedIndex = Math.Max(0, Math.Min(9, settings.DefaultSpeed - 1));
            }
            if (voiceComboBox != null)
            {
                voiceComboBox.SelectedIndex = settings.DefaultVoice == "en_US-amy-low" ? 1 : 0;
            }
            if (autoReadCheck != null)
            {
                autoReadCheck.Checked = settings.AutoReadClipboard;
            }

            // Advanced settings
            enableCacheCheck.Checked = settings.EnablePhonemeCache;
            cacheSizeNumeric.Value = settings.PhonemeCacheSize;
            batchingCheck.Checked = settings.EnableIntelligentChunking;
            enableLoggingCheck.Checked = settings.EnableLogging;

            // Load preset data
            LoadPresetData();

            // Load dictionary data
            LoadDictionaryData();

            // Update row states based on checkboxes
            UpdateHotkeyRowStates();

            Logger.Info("Settings loaded into UI");
        }

        private void LoadHotkeySettings()
        {
            // Load hotkey enabled states
            monitoringEnabledCheck.Checked = settings.MonitoringHotkeyEnabled;
            stopSpeechEnabledCheck.Checked = settings.StopSpeechHotkeyEnabled;
            pauseResumeEnabledCheck.Checked = settings.PauseResumeHotkeyEnabled;
            presetEnabledCheck.Checked = settings.PresetHotkeyEnabled;
            skipForwardEnabledCheck.Checked = settings.SkipForwardHotkeyEnabled;
            skipBackEnabledCheck.Checked = settings.SkipBackHotkeyEnabled;

            // Parse and load hotkey combinations
            ParseAndSetHotkey(settings.MonitoringHotkey, monitoringModifierCombo, monitoringKeyText);
            ParseAndSetHotkey(settings.StopSpeechHotkey, stopSpeechModifierCombo, stopSpeechKeyText);
            ParseAndSetHotkey(settings.PauseResumeHotkey, pauseResumeModifierCombo, pauseResumeKeyText);
            ParseAndSetHotkey(settings.PresetHotkey, presetModifierCombo, presetKeyText);
            ParseAndSetHotkey(settings.SkipForwardHotkey, skipForwardModifierCombo, skipForwardKeyText);
            ParseAndSetHotkey(settings.SkipBackHotkey, skipBackModifierCombo, skipBackKeyText);
        }

        private void ParseAndSetHotkey(string hotkeyString, ComboBox modifierCombo, TextBox keyText)
        {
            if (string.IsNullOrEmpty(hotkeyString) || hotkeyString == "None")
            {
                modifierCombo.SelectedItem = "CTRL+SHIFT";
                keyText.Text = "None";
                return;
            }

            var parts = hotkeyString.Split('+');
            if (parts.Length >= 2)
            {
                // Extract modifier(s)
                var modifiers = new List<string>();
                string key = parts[parts.Length - 1].Trim(); // Last part is the key

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    modifiers.Add(parts[i].Trim().ToUpper());
                }

                string modifierString = string.Join("+", modifiers);
                
                // Set modifier combo to match or default to CTRL+SHIFT
                if (modifierCombo.Items.Contains(modifierString))
                {
                    modifierCombo.SelectedItem = modifierString;
                }
                else
                {
                    modifierCombo.SelectedItem = "CTRL+SHIFT";
                }

                keyText.Text = key;
            }
            else
            {
                modifierCombo.SelectedItem = "CTRL+SHIFT";
                keyText.Text = hotkeyString;
            }
        }

        private void OnHotkeyEnabledChanged(object? sender, EventArgs e)
        {
            UpdateHotkeyRowStates();
        }

        private void UpdateHotkeyRowStates()
        {
            UpdateHotkeyRowState(monitoringModifierCombo, monitoringKeyText, monitoringEnabledCheck);
            UpdateHotkeyRowState(stopSpeechModifierCombo, stopSpeechKeyText, stopSpeechEnabledCheck);
            UpdateHotkeyRowState(pauseResumeModifierCombo, pauseResumeKeyText, pauseResumeEnabledCheck);
            UpdateHotkeyRowState(presetModifierCombo, presetKeyText, presetEnabledCheck);
            UpdateHotkeyRowState(skipForwardModifierCombo, skipForwardKeyText, skipForwardEnabledCheck);
            UpdateHotkeyRowState(skipBackModifierCombo, skipBackKeyText, skipBackEnabledCheck);
        }

        private void UpdateHotkeyRowState(ComboBox modifierCombo, TextBox keyText, CheckBox enabledCheck)
        {
            bool enabled = enabledCheck.Checked;
            Color textColor = enabled ? SystemColors.ControlText : SystemColors.GrayText;
            Color backColor = enabled ? SystemColors.Window : SystemColors.Control;

            modifierCombo.Enabled = enabled;
            keyText.Enabled = enabled;
            
            modifierCombo.ForeColor = textColor;
            keyText.ForeColor = textColor;
            keyText.BackColor = enabled ? SystemColors.Window : SystemColors.Control;
        }

        private void SaveSettings()
        {
            // Menu visibility settings
            settings.ShowMonitoringMenuItem = showMonitoringCheck.Checked;
            settings.ShowStopSpeechMenuItem = showStopSpeechCheck.Checked;
            settings.ShowPauseResumeMenuItem = showPauseResumeCheck.Checked;
            settings.ShowSkipMenuItem = showSkipCheck.Checked;
            settings.ShowSpeedMenuItem = showSpeedCheck.Checked;
            settings.ShowVoiceMenuItem = showVoiceCheck.Checked;
            settings.ShowPresetsMenuItem = showPresetsCheck.Checked;
            settings.ShowExportToWavMenuItem = showExportToWavCheck.Checked;
            
            // Appearance settings
            settings.AnimateTrayIcon = animateTrayIconCheck.Checked;

            // Skip settings
            settings.SkipIntervalSeconds = (int)skipIntervalNumeric.Value;
            
            // Save hotkey settings
            SaveHotkeySettings();

            // Voice settings (legacy - these controls are no longer in the new Presets tab design)
            if (speedComboBox != null)
            {
                settings.DefaultSpeed = speedComboBox.SelectedIndex + 1;
            }
            if (voiceComboBox != null)
            {
                settings.DefaultVoice = voiceComboBox.SelectedIndex == 1 ? "en_US-amy-low" : "en_us-jane-medium";
            }
            if (autoReadCheck != null)
            {
                settings.AutoReadClipboard = autoReadCheck.Checked;
            }

            // Advanced settings
            settings.EnablePhonemeCache = enableCacheCheck.Checked;
            settings.PhonemeCacheSize = (int)cacheSizeNumeric.Value;
            settings.EnableIntelligentChunking = batchingCheck.Checked;
            settings.EnableLogging = enableLoggingCheck.Checked;

            // Apply logging setting to Logger class
            Logger.IsEnabled = settings.EnableLogging;

            settings.Save();
            Logger.Info("Settings saved");
        }

        private void SaveHotkeySettings()
        {
            // Save hotkey enabled states
            settings.MonitoringHotkeyEnabled = monitoringEnabledCheck.Checked;
            settings.StopSpeechHotkeyEnabled = stopSpeechEnabledCheck.Checked;
            settings.PauseResumeHotkeyEnabled = pauseResumeEnabledCheck.Checked;
            settings.PresetHotkeyEnabled = presetEnabledCheck.Checked;
            settings.SkipForwardHotkeyEnabled = skipForwardEnabledCheck.Checked;
            settings.SkipBackHotkeyEnabled = skipBackEnabledCheck.Checked;

            // Save hotkey combinations
            settings.MonitoringHotkey = CombineHotkey(monitoringModifierCombo, monitoringKeyText);
            settings.StopSpeechHotkey = CombineHotkey(stopSpeechModifierCombo, stopSpeechKeyText);
            settings.PauseResumeHotkey = CombineHotkey(pauseResumeModifierCombo, pauseResumeKeyText);
            settings.PresetHotkey = CombineHotkey(presetModifierCombo, presetKeyText);
            settings.SkipForwardHotkey = CombineHotkey(skipForwardModifierCombo, skipForwardKeyText);
            settings.SkipBackHotkey = CombineHotkey(skipBackModifierCombo, skipBackKeyText);
        }

        private string CombineHotkey(ComboBox modifierCombo, TextBox keyText)
        {
            string modifier = modifierCombo.SelectedItem?.ToString() ?? "CTRL+SHIFT";
            string key = keyText.Text.Trim();
            
            if (string.IsNullOrEmpty(key) || key == "None")
            {
                return "None";
            }

            return $"{modifier}+{key}";
        }

        private void NumberButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button button && button.Tag is int presetIndex)
            {
                // Toggle the preset enabled state
                presetEnabled[presetIndex] = !presetEnabled[presetIndex];
                
                // Update the visual state of the button and controls
                UpdatePresetVisualState(presetIndex);
                
                // Remove focus from the button to prevent bold border
                this.Focus();
            }
        }

        private void PresetModelCombo_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (sender is ComboBox modelCombo && modelCombo.Tag is int presetIndex)
            {
                UpdateSpeakerDropdown(presetIndex);
            }
        }

        private void UpdateSpeakerDropdown(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= presetModelCombos.Length)
                return;

            var modelCombo = presetModelCombos[presetIndex];
            var speakerCombo = presetSpeakerCombos[presetIndex];
            
            // Clear existing items
            speakerCombo.Items.Clear();
            
            if (modelCombo.SelectedIndex >= 0 && modelCombo.SelectedItem is string displayName)
            {
                // Get the actual model name from the display name
                if (displayNameToModelMap.TryGetValue(displayName, out string? modelName))
                {
                    // Get available speakers for this model
                    var speakers = VoiceModelDetector.GetAvailableSpeakers(modelName);
                    
                    foreach (var speaker in speakers)
                    {
                        speakerCombo.Items.Add(speaker);
                    }
                    
                    // Select the first speaker by default
                    if (speakerCombo.Items.Count > 0)
                    {
                        speakerCombo.SelectedIndex = 0;
                    }
                }
            }
            
            // If no speakers were added, add a default option
            if (speakerCombo.Items.Count == 0)
            {
                speakerCombo.Items.Add(new SpeakerInfo(0, "Default"));
                speakerCombo.SelectedIndex = 0;
            }
        }

        private void UpdatePresetVisualState(int presetIndex)
        {
            bool enabled = presetEnabled[presetIndex];
            
            // Update button appearance
            if (enabled)
            {
                presetNumberButtons[presetIndex].BackColor = Color.Green;
                presetNumberButtons[presetIndex].ForeColor = Color.White;
            }
            else
            {
                presetNumberButtons[presetIndex].BackColor = SystemColors.Control;
                presetNumberButtons[presetIndex].ForeColor = SystemColors.ControlText;
            }
            
            // Update preset controls
            presetNameTextBoxes[presetIndex].Enabled = enabled;
            presetModelCombos[presetIndex].Enabled = enabled;
            presetSpeakerCombos[presetIndex].Enabled = enabled;
            presetSpeedCombos[presetIndex].Enabled = enabled;
            
            if (enabled)
            {
                presetNameTextBoxes[presetIndex].BackColor = SystemColors.Window;
                presetNameTextBoxes[presetIndex].ForeColor = SystemColors.WindowText;
                presetModelCombos[presetIndex].BackColor = SystemColors.Window;
                presetModelCombos[presetIndex].ForeColor = SystemColors.WindowText;
                presetSpeakerCombos[presetIndex].BackColor = SystemColors.Window;
                presetSpeakerCombos[presetIndex].ForeColor = SystemColors.WindowText;
                presetSpeedCombos[presetIndex].BackColor = SystemColors.Window;
                presetSpeedCombos[presetIndex].ForeColor = SystemColors.WindowText;
            }
            else
            {
                presetNameTextBoxes[presetIndex].BackColor = SystemColors.Control;
                presetNameTextBoxes[presetIndex].ForeColor = SystemColors.GrayText;
                presetModelCombos[presetIndex].BackColor = SystemColors.Control;
                presetModelCombos[presetIndex].ForeColor = SystemColors.GrayText;
                presetSpeakerCombos[presetIndex].BackColor = SystemColors.Control;
                presetSpeakerCombos[presetIndex].ForeColor = SystemColors.GrayText;
                presetSpeedCombos[presetIndex].BackColor = SystemColors.Control;
                presetSpeedCombos[presetIndex].ForeColor = SystemColors.GrayText;
            }
        }

        private void SavePresetData()
        {
            // Sync preset data from UI controls to settings
            for (int i = 0; i < settings.VoicePresets.Length; i++)
            {
                settings.VoicePresets[i].Name = presetNameTextBoxes[i].Text;
                settings.VoicePresets[i].Enabled = presetEnabled[i];
                
                // Get selected model name from display name
                if (presetModelCombos[i].SelectedItem is string displayName &&
                    displayNameToModelMap.TryGetValue(displayName, out string? modelName))
                {
                    settings.VoicePresets[i].Model = modelName;
                }
                
                // Get selected speaker ID
                if (presetSpeakerCombos[i].SelectedItem is SpeakerInfo speakerInfo)
                {
                    settings.VoicePresets[i].SpeakerId = speakerInfo.Id;
                }
                
                // Get selected speed (convert from 1-10 display to actual speed value)
                if (presetSpeedCombos[i].SelectedItem is string speedText &&
                    int.TryParse(speedText, out int speed))
                {
                    settings.VoicePresets[i].Speed = speed;
                }
            }
        }

        private void LoadPresetData()
        {
            // Sync preset data from settings to UI controls
            for (int i = 0; i < Math.Min(settings.VoicePresets.Length, presetNameTextBoxes.Length); i++)
            {
                var preset = settings.VoicePresets[i];
                
                presetNameTextBoxes[i].Text = preset.Name;
                presetEnabled[i] = preset.Enabled;
                
                // Set model selection
                var modelDisplayName = VoiceModelDetector.GetDisplayName(preset.Model);
                var modelIndex = presetModelCombos[i].Items.Cast<string>().ToList().IndexOf(modelDisplayName);
                if (modelIndex >= 0)
                {
                    presetModelCombos[i].SelectedIndex = modelIndex;
                }
                
                // Update speaker dropdown for the selected model
                UpdateSpeakerDropdown(i);
                
                // Set speaker selection
                var speakerItem = presetSpeakerCombos[i].Items.Cast<SpeakerInfo>()
                    .FirstOrDefault(s => s.Id == preset.SpeakerId);
                if (speakerItem != null)
                {
                    presetSpeakerCombos[i].SelectedItem = speakerItem;
                }
                
                // Set speed selection (speed values are 1-10, array indices are 0-9)
                var speedIndex = Math.Max(0, Math.Min(9, preset.Speed - 1));
                presetSpeedCombos[i].SelectedIndex = speedIndex;
                
                // Update visual state
                UpdatePresetVisualState(i);
            }
        }

        private void LoadDictionaryData()
        {
            // Load ignored words
            ignoredWordsListBox.Items.Clear();
            foreach (var word in settings.IgnoredWords)
            {
                ignoredWordsListBox.Items.Add(word);
            }

            // Load replacements
            replacementsListBox.Items.Clear();
            foreach (var replacement in settings.Replacements)
            {
                var displayText = $"{replacement.Key} → {replacement.Value}";
                replacementsListBox.Items.Add(displayText);
            }
        }

        private void SaveDictionaryData()
        {
            // Save ignored words
            settings.IgnoredWords = ignoredWordsListBox.Items.Cast<string>().ToArray();

            // Save replacements
            settings.Replacements.Clear();
            foreach (string item in replacementsListBox.Items)
            {
                var parts = item.Split(" → ");
                if (parts.Length == 2)
                {
                    settings.Replacements[parts[0]] = parts[1];
                }
            }
        }

        // Dictionary event handlers
        private void AddIgnoredWord_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ignoredWordTextBox.Text))
            {
                var word = ignoredWordTextBox.Text.Trim();
                if (!ignoredWordsListBox.Items.Contains(word))
                {
                    ignoredWordsListBox.Items.Add(word);
                    ignoredWordTextBox.Clear();
                }
            }
        }

        private void RemoveIgnoredWord_Click(object? sender, EventArgs e)
        {
            if (ignoredWordsListBox.SelectedItem != null)
            {
                ignoredWordsListBox.Items.Remove(ignoredWordsListBox.SelectedItem);
            }
        }


        private void AddReplacement_Click(object? sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(replacementFromTextBox.Text) && 
                !string.IsNullOrWhiteSpace(replacementToTextBox.Text))
            {
                var from = replacementFromTextBox.Text.Trim();
                var to = replacementToTextBox.Text.Trim();
                var displayText = $"{from} → {to}";
                
                // Check if replacement already exists
                bool exists = false;
                foreach (string item in replacementsListBox.Items)
                {
                    if (item.StartsWith(from + " →"))
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    replacementsListBox.Items.Add(displayText);
                    replacementFromTextBox.Clear();
                    replacementToTextBox.Clear();
                }
            }
        }

        private void RemoveReplacement_Click(object? sender, EventArgs e)
        {
            if (replacementsListBox.SelectedItem != null)
            {
                replacementsListBox.Items.Remove(replacementsListBox.SelectedItem);
            }
        }

        private void ApplySettings()
        {
            SavePresetData(); // Save preset data before saving general settings
            SaveDictionaryData(); // Save dictionary data
            SaveSettings();
            Logger.Info("Settings applied");
        }
    }
}