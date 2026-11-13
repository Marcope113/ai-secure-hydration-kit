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
