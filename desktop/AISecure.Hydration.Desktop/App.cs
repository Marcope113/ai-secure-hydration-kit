using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace AISecure.Hydration.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        // Fluent sliders use the system accent for the thumb, not just Thumb.Background.
        var cyan = Color.Parse("#53DCE8");
        Resources["SystemAccentColor"] = cyan;
        Resources["SystemAccentColorLight1"] = Color.Parse("#85EAF1");
        Resources["SystemAccentColorDark1"] = Color.Parse("#28B9C7");
        Resources["SystemControlForegroundAccentBrush"] = new SolidColorBrush(cyan);
        Resources["SliderThumbBackground"] = new SolidColorBrush(cyan);
        Resources["SliderThumbBackgroundPointerOver"] = new SolidColorBrush(Color.Parse("#85EAF1"));
        Resources["SliderThumbBackgroundPressed"] = new SolidColorBrush(Color.Parse("#28B9C7"));
        // Match slider handles to the Academy cyan accent instead of Fluent's red default.
        Styles.Add(new Style(x => x.OfType<Slider>().Template().OfType<Thumb>())
        {
            Setters = {
                new Setter(Thumb.BackgroundProperty, new SolidColorBrush(Color.Parse("#53DCE8"))),
                new Setter(Thumb.BorderBrushProperty, new SolidColorBrush(Color.Parse("#53DCE8")))
            }
        });
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
