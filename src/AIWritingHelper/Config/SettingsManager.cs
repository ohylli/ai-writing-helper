using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AIWritingHelper.Config;

public class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    private readonly ISerializer _serializer;
    private readonly IDeserializer _deserializer;

    public string SettingsFilePath { get; }

    public SettingsManager(ILogger<SettingsManager> logger)
        : this(logger, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AIWritingHelper", "settings.yaml"))
    {
    }

    public SettingsManager(ILogger<SettingsManager> logger, string settingsFilePath)
    {
        _logger = logger;
        SettingsFilePath = settingsFilePath;

        _serializer = new SerializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                _logger.LogInformation("Settings file not found, using defaults");
                return new AppSettings();
            }

            var yaml = File.ReadAllText(SettingsFilePath);
            var settings = _deserializer.Deserialize<AppSettings>(yaml) ?? new AppSettings();
            NormalizeNullFields(settings);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings file, using defaults");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Ensures no string properties are null after YAML deserialization,
    /// since YamlDotNet can set them to null even when C# declares them non-nullable.
    /// </summary>
    private static void NormalizeNullFields(AppSettings settings)
    {
        var defaults = new AppSettings();
        settings.LlmApiEndpoint ??= defaults.LlmApiEndpoint;
        settings.LlmApiKey ??= defaults.LlmApiKey;
        settings.LlmModelName ??= defaults.LlmModelName;
        settings.LlmSystemPrompt ??= defaults.LlmSystemPrompt;
        settings.SttApiKey ??= defaults.SttApiKey;
        settings.SttModelName ??= defaults.SttModelName;
        settings.TypoFixHotkey ??= defaults.TypoFixHotkey;
        settings.DictationHotkey ??= defaults.DictationHotkey;
        settings.LogLevel ??= defaults.LogLevel;
        settings.MicrophoneDeviceName ??= defaults.MicrophoneDeviceName;
        settings.DictationOutputMode ??= defaults.DictationOutputMode;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(directory);

            var yaml = _serializer.Serialize(settings);
            File.WriteAllText(SettingsFilePath, yaml);
            _logger.LogInformation("Settings saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", SettingsFilePath);
            throw;
        }
    }
}
