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
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
