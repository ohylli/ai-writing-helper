using AIWritingHelper.Config;
using AIWritingHelper.Core;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace AIWritingHelper.UI;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly SettingsManager _settingsManager;
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ILLMProvider _llmProvider;
    private readonly ILogger<SettingsForm> _logger;

    // General tab controls
    private readonly ComboBox _logLevelCombo;
    private readonly TextBox _typoFixHotkeyBox;
    private readonly TextBox _dictationHotkeyBox;

    // Typo Fixing tab controls
    private readonly TextBox _apiEndpointBox;
    private readonly TextBox _apiKeyBox;
    private readonly TextBox _modelNameBox;
    private readonly Button _testConnectionButton;
    private readonly TextBox _systemPromptBox;

    // Buttons
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    public SettingsForm(
        AppSettings settings,
        SettingsManager settingsManager,
        LoggingLevelSwitch levelSwitch,
        ILLMProvider llmProvider,
        ILogger<SettingsForm> logger)
    {
        _settings = settings;
        _settingsManager = settingsManager;
        _levelSwitch = levelSwitch;
        _llmProvider = llmProvider;
        _logger = logger;

        Text = "AI Writing Helper Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ClientSize = new Size(500, 480);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;

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
            AccessibleDescription = "Log level and hotkey display"
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

        // Typo fix hotkey (read-only)
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
            Width = 200,
            AccessibleName = "Typo fix hotkey",
            AccessibleDescription = "Current hotkey for typo fixing. Cannot be changed yet."
        };
        generalLayout.Controls.Add(typoFixHotkeyLabel, 0, 1);
        generalLayout.Controls.Add(_typoFixHotkeyBox, 1, 1);

        // Dictation hotkey (read-only)
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
            Width = 200,
            AccessibleName = "Dictation hotkey",
            AccessibleDescription = "Current hotkey for dictation. Cannot be changed yet."
        };
        generalLayout.Controls.Add(dictationHotkeyLabel, 0, 2);
        generalLayout.Controls.Add(_dictationHotkeyBox, 1, 2);

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
            AccessibleDescription = "Dictation configuration"
        };
        var dictationLabel = new Label
        {
            Text = "Dictation settings will be available in a future update.",
            AutoSize = true,
            Padding = new Padding(10),
            AccessibleName = "Dictation settings placeholder",
            AccessibleDescription = "Dictation settings are not yet available"
        };
        dictationTab.Controls.Add(dictationLabel);

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

        // Add button panel before tab control so it docks at the bottom
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

        // Typo Fixing
        _apiEndpointBox.Text = _settings.LlmApiEndpoint;
        _apiKeyBox.Text = _settings.LlmApiKey;
        _modelNameBox.Text = _settings.LlmModelName;
        _systemPromptBox.Text = _settings.LlmSystemPrompt;
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        // Write form values back to the shared AppSettings singleton
        _settings.LogLevel = _logLevelCombo.SelectedItem?.ToString() ?? "Information";
        _settings.LlmApiEndpoint = _apiEndpointBox.Text;
        _settings.LlmApiKey = _apiKeyBox.Text;
        _settings.LlmModelName = _modelNameBox.Text;
        _settings.LlmSystemPrompt = _systemPromptBox.Text;

        // Hot-reload log level
        if (Enum.TryParse<LogEventLevel>(_settings.LogLevel, ignoreCase: true, out var parsedLevel))
        {
            _levelSwitch.MinimumLevel = parsedLevel;
        }

        try
        {
            _settingsManager.Save(_settings);
            _logger.LogInformation("Settings saved via GUI");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Save Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private async void OnTestConnectionClick(object? sender, EventArgs e)
    {
        // Snapshot current settings
        var originalEndpoint = _settings.LlmApiEndpoint;
        var originalKey = _settings.LlmApiKey;
        var originalModel = _settings.LlmModelName;

        // Temporarily apply form values
        _settings.LlmApiEndpoint = _apiEndpointBox.Text;
        _settings.LlmApiKey = _apiKeyBox.Text;
        _settings.LlmModelName = _modelNameBox.Text;

        _testConnectionButton.Enabled = false;
        _testConnectionButton.Text = "Testing...";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _llmProvider.FixTextAsync("Hello", "Reply with OK", cts.Token);

            MessageBox.Show(
                "Connection successful!",
                "Test Connection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test connection failed");
            MessageBox.Show(
                $"Connection failed: {ex.Message}",
                "Test Connection",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            // Restore original settings
            _settings.LlmApiEndpoint = originalEndpoint;
            _settings.LlmApiKey = originalKey;
            _settings.LlmModelName = originalModel;

            _testConnectionButton.Enabled = true;
            _testConnectionButton.Text = "&Test Connection";
        }
    }
}
