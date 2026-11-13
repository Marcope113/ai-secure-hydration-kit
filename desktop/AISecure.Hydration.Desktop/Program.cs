using Avalonia;

namespace AISecure.Hydration.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("HYDRATION DESKTOP STARTUP ERROR:");
            Console.Error.WriteLine(ex.ToString());
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AISecureAcademy", "logs");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "desktop-startup.log");
                File.AppendAllText(file, $"{DateTimeOffset.Now:o} {ex}{Environment.NewLine}");
                Console.Error.WriteLine("Log: " + file);
            }
            catch (Exception logError) { Console.Error.WriteLine("Could not write log: " + logError.Message); }
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
