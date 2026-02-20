using AIWritingHelper.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIWritingHelper.Tests.Config;

public class SettingsManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SettingsManager _manager;

    public SettingsManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AIWritingHelperTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        var settingsPath = Path.Combine(_tempDir, "settings.yaml");
        var logger = NullLoggerFactory.Instance.CreateLogger<SettingsManager>();
        _manager = new SettingsManager(logger, settingsPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = _manager.Load();

        Assert.Equal("https://api.cerebras.ai/v1", settings.LlmApiEndpoint);
        Assert.Equal("", settings.LlmApiKey);
        Assert.Equal("llama-4-scout-17b-16e-instruct", settings.LlmModelName);
        Assert.Equal("Ctrl+Alt+Space", settings.TypoFixHotkey);
        Assert.Equal("Ctrl+Alt+D", settings.DictationHotkey);
        Assert.False(settings.StartWithWindows);
        Assert.Equal("Information", settings.LogLevel);
        Assert.Equal("", settings.MicrophoneDeviceName);
        Assert.Equal("Clipboard", settings.DictationOutputMode);
        Assert.Equal("scribe_v2", settings.SttModelName);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip_AllFieldsMatch()
    {
        var original = new AppSettings
        {
            LlmApiEndpoint = "https://custom.api/v1",
            LlmApiKey = "test-key-123",
            LlmModelName = "custom-model",
            LlmSystemPrompt = "Custom prompt",
            SttApiKey = "stt-key-456",
            SttModelName = "custom_stt",
            TypoFixHotkey = "Ctrl+Shift+F",
            DictationHotkey = "Ctrl+Shift+D",
            StartWithWindows = true,
            LogLevel = "Debug",
            MicrophoneDeviceName = "My Microphone",
            DictationOutputMode = "DirectInsertion",
        };

        _manager.Save(original);
        var loaded = _manager.Load();

        Assert.Equal(original.LlmApiEndpoint, loaded.LlmApiEndpoint);
        Assert.Equal(original.LlmApiKey, loaded.LlmApiKey);
        Assert.Equal(original.LlmModelName, loaded.LlmModelName);
        Assert.Equal(original.LlmSystemPrompt, loaded.LlmSystemPrompt);
        Assert.Equal(original.SttApiKey, loaded.SttApiKey);
        Assert.Equal(original.SttModelName, loaded.SttModelName);
        Assert.Equal(original.TypoFixHotkey, loaded.TypoFixHotkey);
        Assert.Equal(original.DictationHotkey, loaded.DictationHotkey);
        Assert.Equal(original.StartWithWindows, loaded.StartWithWindows);
        Assert.Equal(original.LogLevel, loaded.LogLevel);
        Assert.Equal(original.MicrophoneDeviceName, loaded.MicrophoneDeviceName);
        Assert.Equal(original.DictationOutputMode, loaded.DictationOutputMode);
    }

    [Fact]
    public void Load_CorruptYaml_ReturnsDefaults()
    {
        File.WriteAllText(_manager.SettingsFilePath, "{{{{not valid yaml::::}}}}");

        var settings = _manager.Load();

        Assert.Equal("https://api.cerebras.ai/v1", settings.LlmApiEndpoint);
    }

    [Fact]
    public void Load_PartialYaml_MissingFieldsGetDefaults()
    {
        File.WriteAllText(_manager.SettingsFilePath, "LlmApiKey: partial-key\nLogLevel: Debug\n");

        var settings = _manager.Load();

        Assert.Equal("partial-key", settings.LlmApiKey);
        Assert.Equal("Debug", settings.LogLevel);
        // Missing fields should have defaults
        Assert.Equal("https://api.cerebras.ai/v1", settings.LlmApiEndpoint);
        Assert.Equal("Ctrl+Alt+Space", settings.TypoFixHotkey);
        Assert.Equal("Clipboard", settings.DictationOutputMode);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNeeded()
    {
        var nestedDir = Path.Combine(_tempDir, "sub", "dir");
        var nestedPath = Path.Combine(nestedDir, "settings.yaml");
        var logger = NullLoggerFactory.Instance.CreateLogger<SettingsManager>();
        var manager = new SettingsManager(logger, nestedPath);

        manager.Save(new AppSettings());

        Assert.True(File.Exists(nestedPath));
    }
}
