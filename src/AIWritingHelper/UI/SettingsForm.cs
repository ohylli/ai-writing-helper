using System.Runtime.InteropServices;
using AIWritingHelper.Audio;
using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace AIWritingHelper.UI;

internal sealed class SettingsForm : Form
{
    [DllImport("user32.dll")]
    private static extern void NotifyWinEvent(uint winEvent, IntPtr hwnd, int objId, int childId);

    private const uint EVENT_OBJECT_STATECHANGE = 0x800A;
    private const int OBJID_CLIENT = -4;
    private const int CHILDID_SELF = 0;

    private const string DefaultMicrophoneLabel = "(Default)";

    private readonly AppSettings _settings;
    private readonly SettingsManager _settingsManager;
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ILLMProvider _llmProvider;
    private readonly ISTTProvider _sttProvider;
    private readonly IAudioRecorder _audioRecorder;
    private readonly GlobalHotkeyManager _hotkeyManager;
    private readonly IStartupManager _startupManager;
    private readonly ILogger<SettingsForm> _logger;

    // General tab controls
    private readonly ComboBox _logLevelCombo;
    private readonly TextBox _typoFixHotkeyBox;
    private readonly TextBox _dictationHotkeyBox;
    private readonly Button _setTypoFixHotkeyButton;
    private readonly Button _setDictationHotkeyButton;
    private readonly CheckBox _startWithWindowsCheckBox;

    // Hotkey capture state
    private TextBox? _captureTargetBox;
    private Button? _captureSourceButton;
    private string? _captureOriginalText;
    private string? _captureOriginalAccessibleName;
    private string? _captureSourceButtonOriginalAccessibleName;
    private string? _captureSourceButtonOriginalAccessibleDescription;

    // Typo Fixing tab controls
    private readonly TextBox _apiEndpointBox;
    private readonly TextBox _apiKeyBox;
    private readonly TextBox _modelNameBox;
    private readonly Button _testConnectionButton;
    private readonly TextBox _systemPromptBox;

    // Dictation tab controls
    private readonly TextBox _sttApiKeyBox;
    private readonly TextBox _sttModelNameBox;
    private readonly Button _testDictationButton;
    private readonly ComboBox _microphoneCombo;

    // Buttons
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    public SettingsForm(
        AppSettings settings,
        SettingsManager settingsManager,
        LoggingLevelSwitch levelSwitch,
        ILLMProvider llmProvider,
        ISTTProvider sttProvider,
        IAudioRecorder audioRecorder,
        GlobalHotkeyManager hotkeyManager,
        IStartupManager startupManager,
        ILogger<SettingsForm> logger)
    {
        _settings = settings;
        _settingsManager = settingsManager;
        _levelSwitch = levelSwitch;
        _llmProvider = llmProvider;
        _sttProvider = sttProvider;
        _audioRecorder = audioRecorder;
        _hotkeyManager = hotkeyManager;
        _startupManager = startupManager;
        _logger = logger;

        Text = "AI Writing Helper Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(500, 480);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        KeyDown += OnFormKeyDown;

        // Tab control
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Settings tabs",
            AccessibleDescription = "Use arrow keys to switch between tabs"
        };

        // --- General tab ---
        var generalTab = new TabPage("General")
        {
            AccessibleName = "General settings",
            AccessibleDescription = "Log level and hotkey configuration"
        };
        var generalLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10),
            AutoScroll = true
        };
        generalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        generalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Log level
        var logLevelLabel = new Label
        {
            Text = "Log level:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _logLevelCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
            AccessibleName = "Log level",
            AccessibleDescription = "Minimum severity of log messages to record"
        };
        _logLevelCombo.Items.AddRange(["Debug", "Information", "Warning", "Error"]);
        generalLayout.Controls.Add(logLevelLabel, 0, 0);
        generalLayout.Controls.Add(_logLevelCombo, 1, 0);

        // Typo fix hotkey
        var typoFixHotkeyLabel = new Label
        {
            Text = "Typo fix hotkey:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _typoFixHotkeyBox = new TextBox
        {
            ReadOnly = true,
            Width = 160,
            AccessibleName = "Typo fix hotkey",
            AccessibleDescription = "Current hotkey for typo fixing"
        };
        _setTypoFixHotkeyButton = new Button
        {
            Text = "Set New Hotkey",
            AutoSize = true,
            AccessibleName = "Set typo fix hotkey",
            AccessibleDescription = "Enter capture mode to record a new hotkey for typo fixing"
        };
        _setTypoFixHotkeyButton.Click += (_, _) => EnterCaptureMode(_typoFixHotkeyBox, _setTypoFixHotkeyButton);
        var typoHotkeyPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false
        };
        typoHotkeyPanel.Controls.Add(_typoFixHotkeyBox);
        typoHotkeyPanel.Controls.Add(_setTypoFixHotkeyButton);
        generalLayout.Controls.Add(typoFixHotkeyLabel, 0, 1);
        generalLayout.Controls.Add(typoHotkeyPanel, 1, 1);

        // Dictation hotkey
        var dictationHotkeyLabel = new Label
        {
            Text = "Dictation hotkey:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _dictationHotkeyBox = new TextBox
        {
            ReadOnly = true,
            Width = 160,
            AccessibleName = "Dictation hotkey",
            AccessibleDescription = "Current hotkey for dictation"
        };
        _setDictationHotkeyButton = new Button
        {
            Text = "Set New Hotkey",
            AutoSize = true,
            AccessibleName = "Set dictation hotkey",
            AccessibleDescription = "Enter capture mode to record a new hotkey for dictation"
        };
        _setDictationHotkeyButton.Click += (_, _) => EnterCaptureMode(_dictationHotkeyBox, _setDictationHotkeyButton);
        var dictationHotkeyPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false
        };
        dictationHotkeyPanel.Controls.Add(_dictationHotkeyBox);
        dictationHotkeyPanel.Controls.Add(_setDictationHotkeyButton);
        generalLayout.Controls.Add(dictationHotkeyLabel, 0, 2);
        generalLayout.Controls.Add(dictationHotkeyPanel, 1, 2);

        // Start with Windows
        _startWithWindowsCheckBox = new CheckBox
        {
            Text = "Start with Windows",
            AutoSize = true,
            AccessibleName = "Start with Windows",
            AccessibleDescription = "When checked, AI Writing Helper launches automatically when you sign in to Windows"
        };
        _startWithWindowsCheckBox.CheckStateChanged += (sender, e) =>
        {
            var box = (CheckBox)sender!;
            if (box.IsHandleCreated && box.Focused)
                NotifyWinEvent(EVENT_OBJECT_STATECHANGE, box.Handle, OBJID_CLIENT, CHILDID_SELF);
        };
        generalLayout.Controls.Add(_startWithWindowsCheckBox, 1, 3);

        generalTab.Controls.Add(generalLayout);

        // --- Typo Fixing tab ---
        var typoFixTab = new TabPage("Typo Fixing")
        {
            AccessibleName = "Typo fixing settings",
            AccessibleDescription = "API credentials, model, and system prompt"
        };
        var typoLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Padding = new Padding(10)
        };
        typoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        typoLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        typoLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // API endpoint
        typoLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // API key
        typoLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Model name
        typoLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Test Connection
        typoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // System prompt

        // API endpoint
        var apiEndpointLabel = new Label
        {
            Text = "API endpoint:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _apiEndpointBox = new TextBox
        {
            Dock = DockStyle.Fill,
            AccessibleName = "API endpoint",
            AccessibleDescription = "Base URL for the OpenAI-compatible LLM API"
        };
        typoLayout.Controls.Add(apiEndpointLabel, 0, 0);
        typoLayout.Controls.Add(_apiEndpointBox, 1, 0);

        // API key
        var apiKeyLabel = new Label
        {
            Text = "API key:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _apiKeyBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            AccessibleName = "API key",
            AccessibleDescription = "Secret API key for the LLM service. Characters are hidden."
        };
        typoLayout.Controls.Add(apiKeyLabel, 0, 1);
        typoLayout.Controls.Add(_apiKeyBox, 1, 1);

        // Model name
        var modelNameLabel = new Label
        {
            Text = "Model name:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _modelNameBox = new TextBox
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Model name",
            AccessibleDescription = "Name of the LLM model to use for typo fixing"
        };
        typoLayout.Controls.Add(modelNameLabel, 0, 2);
        typoLayout.Controls.Add(_modelNameBox, 1, 2);

        // Test Connection button
        _testConnectionButton = new Button
        {
            Text = "&Test Connection",
            AutoSize = true,
            AccessibleName = "Test Connection",
            AccessibleDescription = "Send a test request to verify API credentials work"
        };
        _testConnectionButton.Click += OnTestConnectionClick;
        typoLayout.Controls.Add(_testConnectionButton, 1, 3);

        // System prompt
        var systemPromptLabel = new Label
        {
            Text = "System prompt:",
            AutoSize = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top,
            TabStop = false
        };
        _systemPromptBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            AccessibleName = "System prompt",
            AccessibleDescription = "Instructions sent to the LLM before your text. Controls how corrections are made."
        };
        typoLayout.Controls.Add(systemPromptLabel, 0, 4);
        typoLayout.Controls.Add(_systemPromptBox, 1, 4);

        typoFixTab.Controls.Add(typoLayout);

        // --- Dictation tab ---
        var dictationTab = new TabPage("Dictation")
        {
            AccessibleName = "Dictation settings",
            AccessibleDescription = "Speech-to-text credentials, model, and microphone selection"
        };
        var dictationLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };
        dictationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        dictationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dictationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // STT API key
        dictationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // STT model name
        dictationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Test Connection
        dictationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Microphone

        // STT API key
        var sttApiKeyLabel = new Label
        {
            Text = "API key:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _sttApiKeyBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            AccessibleName = "Speech to text API key",
            AccessibleDescription = "Secret API key for the speech-to-text service. Characters are hidden."
        };
        dictationLayout.Controls.Add(sttApiKeyLabel, 0, 0);
        dictationLayout.Controls.Add(_sttApiKeyBox, 1, 0);

        // STT model name
        var sttModelLabel = new Label
        {
            Text = "Model name:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _sttModelNameBox = new TextBox
        {
            Dock = DockStyle.Fill,
            AccessibleName = "Speech to text model name",
            AccessibleDescription = "Name of the speech-to-text model to use for dictation"
        };
        dictationLayout.Controls.Add(sttModelLabel, 0, 1);
        dictationLayout.Controls.Add(_sttModelNameBox, 1, 1);

        // Test Connection button
        _testDictationButton = new Button
        {
            Text = "&Test Connection",
            AutoSize = true,
            AccessibleName = "Test dictation connection",
            AccessibleDescription = "Send a short silent test request to verify speech-to-text credentials work"
        };
        _testDictationButton.Click += OnTestDictationClick;
        dictationLayout.Controls.Add(_testDictationButton, 1, 2);

        // Microphone dropdown
        var microphoneLabel = new Label
        {
            Text = "Microphone:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            TabStop = false
        };
        _microphoneCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            AccessibleName = "Microphone",
            AccessibleDescription = "Microphone device used for dictation. Select Default to use the system default device."
        };
        _microphoneCombo.Items.Add(DefaultMicrophoneLabel);
        try
        {
            foreach (var dev in _audioRecorder.EnumerateDevices())
                _microphoneCombo.Items.Add(dev.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate microphones");
        }
        dictationLayout.Controls.Add(microphoneLabel, 0, 3);
        dictationLayout.Controls.Add(_microphoneCombo, 1, 3);

        dictationTab.Controls.Add(dictationLayout);

        tabControl.TabPages.Add(generalTab);
        tabControl.TabPages.Add(typoFixTab);
        tabControl.TabPages.Add(dictationTab);

        // --- Save / Cancel buttons ---
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            Padding = new Padding(5)
        };

        _cancelButton = new Button
        {
            Text = "&Cancel",
            DialogResult = DialogResult.Cancel,
            AccessibleName = "Cancel",
            AccessibleDescription = "Close without saving changes"
        };

        _saveButton = new Button
        {
            Text = "&Save",
            AccessibleName = "Save",
            AccessibleDescription = "Save all settings and close"
        };
        _saveButton.Click += OnSaveClick;

        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_saveButton);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        // WinForms docks controls in reverse collection order: buttonPanel (added last)
        // docks to the bottom first, then tabControl fills the remaining space.
        Controls.Add(tabControl);
        Controls.Add(buttonPanel);

        PopulateFromSettings();
    }

    private void PopulateFromSettings()
    {
        // General
        var levelIndex = _logLevelCombo.Items.IndexOf(_settings.LogLevel);
        _logLevelCombo.SelectedIndex = levelIndex >= 0 ? levelIndex : 1; // default to Information
        _typoFixHotkeyBox.Text = _settings.TypoFixHotkey;
        _dictationHotkeyBox.Text = _settings.DictationHotkey;
        _startWithWindowsCheckBox.Checked = _settings.StartWithWindows;

        // Typo Fixing
        _apiEndpointBox.Text = _settings.LlmApiEndpoint;
        _apiKeyBox.Text = _settings.LlmApiKey;
        _modelNameBox.Text = _settings.LlmModelName;
        _systemPromptBox.Text = _settings.LlmSystemPrompt;

        // Dictation
        _sttApiKeyBox.Text = _settings.SttApiKey;
        _sttModelNameBox.Text = _settings.SttModelName;
        SelectMicrophoneByName(_settings.MicrophoneDeviceName);
    }

    private void SelectMicrophoneByName(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName))
        {
            _microphoneCombo.SelectedIndex = 0;
            return;
        }

        var index = _microphoneCombo.Items.IndexOf(deviceName);
        if (index < 0)
        {
            // Saved device isn't currently present (e.g., USB mic unplugged).
            // Add it so save round-trips don't silently change the user's setting.
            index = _microphoneCombo.Items.Add(deviceName);
        }
        _microphoneCombo.SelectedIndex = index;
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        // Build candidate settings without touching the live singleton
        var candidate = new AppSettings();
        candidate.CopyFrom(_settings);
        candidate.LogLevel = _logLevelCombo.SelectedItem?.ToString() ?? "Information";
        candidate.TypoFixHotkey = _typoFixHotkeyBox.Text;
        candidate.DictationHotkey = _dictationHotkeyBox.Text;
        candidate.LlmApiEndpoint = _apiEndpointBox.Text;
        candidate.LlmApiKey = _apiKeyBox.Text;
        candidate.LlmModelName = _modelNameBox.Text;
        candidate.LlmSystemPrompt = _systemPromptBox.Text;
        candidate.SttApiKey = _sttApiKeyBox.Text;
        candidate.SttModelName = _sttModelNameBox.Text;
        candidate.MicrophoneDeviceName = _microphoneCombo.SelectedIndex <= 0
            ? ""
            : _microphoneCombo.SelectedItem?.ToString() ?? "";
        candidate.StartWithWindows = _startWithWindowsCheckBox.Checked;

        // Validate hotkeys before persisting — if registration fails, nothing is saved
        bool hotkeysChanged =
            candidate.TypoFixHotkey != _settings.TypoFixHotkey ||
            candidate.DictationHotkey != _settings.DictationHotkey;

        if (hotkeysChanged)
        {
            var oldTypoFix = _settings.TypoFixHotkey;
            var oldDictation = _settings.DictationHotkey;

            _hotkeyManager.UnregisterAll();

            var failures = new List<string>();
            TryRegisterHotkey(GlobalHotkeyManager.TypoFixHotkeyId, candidate.TypoFixHotkey, "Typo Fix", failures);
            TryRegisterHotkey(GlobalHotkeyManager.DictationHotkeyId, candidate.DictationHotkey, "Dictation", failures);

            if (failures.Count > 0)
            {
                _logger.LogWarning("Hotkey registration failed: {Failures}", string.Join(", ", failures));

                // Roll back: unregister whatever succeeded, re-register originals
                _hotkeyManager.UnregisterAll();
                var rollbackFailures = new List<string>();
                TryRegisterHotkey(GlobalHotkeyManager.TypoFixHotkeyId, oldTypoFix, "Typo Fix", rollbackFailures);
                TryRegisterHotkey(GlobalHotkeyManager.DictationHotkeyId, oldDictation, "Dictation", rollbackFailures);

                // Revert UI
                _typoFixHotkeyBox.Text = oldTypoFix;
                _dictationHotkeyBox.Text = oldDictation;

                var message = "Could not register the following hotkey(s) — they may be in use by another application:\n\n" +
                    string.Join("\n", failures) +
                    "\n\nNo settings were saved.";

                if (rollbackFailures.Count > 0)
                {
                    _logger.LogError("Hotkey rollback also failed: {Failures}", string.Join(", ", rollbackFailures));
                    message += "\n\nWarning: the previous hotkeys could not be restored either. " +
                        "Please set new hotkeys and save again.";
                }
                else
                {
                    message += " The previous hotkeys have been restored.";
                }

                MessageBox.Show(this, message, "Hotkey Registration Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        // Hotkeys OK (or unchanged) — persist to disk
        try
        {
            _settingsManager.Save(candidate);
            _logger.LogInformation("Settings saved via GUI");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            MessageBox.Show(this,
                $"Failed to save settings: {ex.Message}",
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // Commit to the live singleton
        _settings.CopyFrom(candidate);

        // Sync Windows startup registry entry
        if (!_startupManager.SyncStartupState())
        {
            MessageBox.Show(this, "Settings were saved, but the Windows startup entry could not be updated.",
                "Startup Registration Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Hot-reload log level
        if (Enum.TryParse<LogEventLevel>(_settings.LogLevel, ignoreCase: true, out var parsedLevel))
        {
            _levelSwitch.MinimumLevel = parsedLevel;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void TryRegisterHotkey(int id, string hotkeyString, string label, List<string>? failures)
    {
        try
        {
            if (!_hotkeyManager.Register(id, hotkeyString))
                failures?.Add($"{label} ({hotkeyString})");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid hotkey for {Label}: {Hotkey}", label, hotkeyString);
            failures?.Add($"{label} ({hotkeyString}) — invalid format");
        }
    }

    private void EnterCaptureMode(TextBox targetBox, Button sourceButton)
    {
        // If already capturing for another hotkey, cancel that first
        if (_captureTargetBox is not null)
        {
            _captureTargetBox.Text = _captureOriginalText ?? "";
            ExitCaptureMode();
        }

        _captureTargetBox = targetBox;
        _captureSourceButton = sourceButton;
        _captureSourceButtonOriginalAccessibleName = sourceButton.AccessibleName;
        _captureSourceButtonOriginalAccessibleDescription = sourceButton.AccessibleDescription;
        _captureOriginalText = targetBox.Text;
        _captureOriginalAccessibleName = targetBox.AccessibleName;

        sourceButton.Text = "Press the new hotkey";
        sourceButton.AccessibleName = "Press the new hotkey";
        sourceButton.AccessibleDescription = "";

        _saveButton.Enabled = false;

        targetBox.Text = "";
        targetBox.AccessibleName = $"Press a new {_captureOriginalAccessibleName} combination, or Escape to cancel";
        targetBox.Focus();
    }

    private void ExitCaptureMode()
    {
        if (_captureTargetBox is not null)
            _captureTargetBox.AccessibleName = _captureOriginalAccessibleName;

        if (_captureSourceButton is not null)
        {
            _captureSourceButton.Text = "Set New Hotkey";
            _captureSourceButton.AccessibleName = _captureSourceButtonOriginalAccessibleName;
            _captureSourceButton.AccessibleDescription = _captureSourceButtonOriginalAccessibleDescription;
        }
            
        _captureTargetBox = null;
        _captureSourceButton = null;
        _captureOriginalText = null;
        _captureOriginalAccessibleName = null;
        _captureSourceButtonOriginalAccessibleName = null;
        _captureSourceButtonOriginalAccessibleDescription = null;

        _saveButton.Enabled = true;
    }

    // ProcessDialogKey runs before CancelButton handling, so we can intercept
    // Escape during hotkey capture before WinForms closes the dialog.
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (_captureTargetBox is not null && (keyData & Keys.KeyCode) == Keys.Escape)
        {
            _captureTargetBox.Text = _captureOriginalText ?? "";
            var box = _captureTargetBox;
            var name = _captureOriginalAccessibleName;
            ExitCaptureMode();
            box.Focus();

            box.AccessibilityObject.RaiseAutomationNotification(
                System.Windows.Forms.Automation.AutomationNotificationKind.ActionAborted,
                System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent,
                $"{name} capture cancelled");

            return true; // swallow the key
        }

        return base.ProcessDialogKey(keyData);
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_captureTargetBox is null)
            return;

        // Ignore standalone modifier key presses — wait for a real key
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu
            or Keys.LControlKey or Keys.RControlKey
            or Keys.LShiftKey or Keys.RShiftKey
            or Keys.LMenu or Keys.RMenu
            or Keys.LWin or Keys.RWin)
        {
            return;
        }

        // Require at least one modifier
        if (e.Modifiers == Keys.None)
            return;

        e.Handled = true;
        e.SuppressKeyPress = true;

        var hotkeyText = FormatHotkey(e.Modifiers, e.KeyCode);
        _captureTargetBox.Text = hotkeyText;
        var capturedBox = _captureTargetBox;
        ExitCaptureMode();
        capturedBox.Focus();

        // Announce the result to screen readers
        capturedBox.AccessibilityObject.RaiseAutomationNotification(
            System.Windows.Forms.Automation.AutomationNotificationKind.ActionCompleted,
            System.Windows.Forms.Automation.AutomationNotificationProcessing.ImportantMostRecent,
            $"{capturedBox.AccessibleName} set to {hotkeyText}");
    }

    internal static string FormatHotkey(Keys modifiers, Keys keyCode)
    {
        var parts = new List<string>();
        if ((modifiers & Keys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & Keys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & Keys.Shift) != 0) parts.Add("Shift");
        parts.Add(keyCode.ToString());
        return string.Join("+", parts);
    }

    private async void OnTestConnectionClick(object? sender, EventArgs e)
    {
        _testConnectionButton.Enabled = false;
        _testConnectionButton.Text = "Testing...";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _llmProvider.FixTextAsync(
                "Hello", "Reply with OK",
                _apiEndpointBox.Text, _apiKeyBox.Text, _modelNameBox.Text,
                cts.Token);

            MessageBox.Show(this,
                "Connection successful!",
                "Test Connection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test connection failed");
            MessageBox.Show(this,
                $"Connection failed: {ex.Message}",
                "Test Connection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed)
            {
                _testConnectionButton.Enabled = true;
                _testConnectionButton.Text = "&Test Connection";
            }
        }
    }

    private async void OnTestDictationClick(object? sender, EventArgs e)
    {
        _testDictationButton.Enabled = false;
        _testDictationButton.Text = "Testing...";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var silentAudio = SilentWavGenerator.CreateSilentWav(TimeSpan.FromMilliseconds(500));
            // Empty transcript on silent audio is success — auth + model both worked.
            await _sttProvider.TranscribeAsync(
                silentAudio,
                _sttApiKeyBox.Text,
                _sttModelNameBox.Text,
                cts.Token);

            MessageBox.Show(this,
                "Connection successful!",
                "Test Connection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dictation test connection failed");
            MessageBox.Show(this,
                $"Connection failed: {ex.Message}",
                "Test Connection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed)
            {
                _testDictationButton.Enabled = true;
                _testDictationButton.Text = "&Test Connection";
            }
        }
    }
}
