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
            var settings = _deserializer.Deserialize<AppSettings>(yaml);
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings file, using defaults");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsFilePath)!;
        Directory.CreateDirectory(directory);

        var yaml = _serializer.Serialize(settings);
        File.WriteAllText(SettingsFilePath, yaml);
        _logger.LogInformation("Settings saved");
    }
}
