using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
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
            return;
        }

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AIWritingHelper", "logs");
        Directory.CreateDirectory(logDir);

        CleanOldLogs(logDir);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDir, $"log-{timestamp}.txt"))
            .CreateLogger();

        try
        {
            Log.Information("AI Writing Helper starting");

            var appCts = new CancellationTokenSource();
            Application.ApplicationExit += (_, _) => appCts.Cancel();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog());
            services.AddSingleton(appCts);
            var provider = services.BuildServiceProvider();

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext(provider.GetRequiredService<ILogger<TrayApplicationContext>>()));

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
                .OrderByDescending(f => f)
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
