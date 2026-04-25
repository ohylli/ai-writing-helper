using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using AIWritingHelper.Audio;
using AIWritingHelper.Config;
using AIWritingHelper.Core;
using AIWritingHelper.Services;
using AIWritingHelper.UI;

namespace AIWritingHelper;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, @"Global\AIWritingHelper", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "AI Writing Helper is already running.",
                "Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AIWritingHelper", "logs");
        Directory.CreateDirectory(logDir);

        CleanOldLogs(logDir);

        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .WriteTo.File(Path.Combine(logDir, $"log-{timestamp}.txt"))
            .CreateLogger();

        try
        {
            Log.Information("AI Writing Helper starting");

            // Load settings early so we can apply log level and register in DI.
            // Create a dedicated factory that hooks into the already-configured Serilog static logger.
            using var earlyLoggerFactory = LoggerFactory.Create(b => b.AddSerilog());
            var settingsManager = new SettingsManager(
                earlyLoggerFactory.CreateLogger<SettingsManager>());
            var appSettings = settingsManager.Load();

            if (Enum.TryParse<LogEventLevel>(appSettings.LogLevel, ignoreCase: true, out var parsedLevel))
            {
                levelSwitch.MinimumLevel = parsedLevel;
            }

            using var appCts = new CancellationTokenSource();
            Application.ApplicationExit += (_, _) => appCts.Cancel();

            ApplicationConfiguration.Initialize();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog());
            services.AddSingleton(appCts);
            services.AddSingleton(levelSwitch);
            services.AddSingleton(settingsManager);
            services.AddSingleton(appSettings);
            services.AddSingleton<TrayApplicationContext>();
            services.AddSingleton<ITrayNotifier>(sp => sp.GetRequiredService<TrayApplicationContext>());
            services.AddSingleton<ISoundPlayer, SystemSoundPlayer>();
            services.AddSingleton<IAudioRecorder, MicrophoneRecorder>();
            services.AddSingleton<IClipboardService, ClipboardService>();
            services.AddHttpClient();
            services.AddSingleton<ILLMProvider, OpenAICompatibleLLMProvider>();
            services.AddSingleton<ISTTProvider, ElevenLabsSTTProvider>();
            services.AddSingleton<OperationLock>();
            services.AddSingleton<TypoFixService>();
            services.AddSingleton<DictationService>();
            services.AddSingleton<IStartupManager, WindowsStartupManager>();
            // Lazy<T> breaks a circular DI dependency: TrayApplicationContext → service
            // → ITrayNotifier → TrayApplicationContext. Resolved lazily on first hotkey press.
            services.AddSingleton(sp => new Lazy<TypoFixService>(sp.GetRequiredService<TypoFixService>));
            services.AddSingleton(sp => new Lazy<DictationService>(sp.GetRequiredService<DictationService>));
            services.AddSingleton<GlobalHotkeyManager>();
            using var provider = services.BuildServiceProvider();

            if (!provider.GetRequiredService<IStartupManager>().SyncStartupState())
                Log.Warning("Startup registry sync failed on launch");

            Application.Run(provider.GetRequiredService<TrayApplicationContext>());

            Log.Information("AI Writing Helper exiting");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void CleanOldLogs(string logDir)
    {
        try
        {
            var oldLogs = Directory.GetFiles(logDir, "log-*.txt")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(3);
            foreach (var f in oldLogs)
            {
                File.Delete(f);
            }
        }
        catch
        {
            // Non-critical — don't fail startup over log cleanup
        }
    }
}
