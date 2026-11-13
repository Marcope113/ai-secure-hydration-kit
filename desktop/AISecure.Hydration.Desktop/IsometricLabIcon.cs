using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AISecure.Hydration.Desktop;

/// <summary>Small isometric vector experiment for the lab cards; no bitmap assets.</summary>
public sealed class IsometricLabIcon : Control
{
    public string Kind { get; set; } = "computer";

    public IsometricLabIcon()
    {
        Width = 38;
        Height = 34;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var outline = new Pen(Brush("#69DDEB"), 1.3);
        void Shape(string path, string color) =>
            context.DrawGeometry(Brush(color), outline, Geometry.Parse(path));

        if (Kind == "network")
        {
            // Isometric switch: top, front, right side and three tiny ports.
            Shape("M 4,12 L 21,5 L 35,12 L 18,20 Z", "#2B6C82");
            Shape("M 4,12 L 18,20 L 18,29 L 4,21 Z", "#174158");
            Shape("M 18,20 L 35,12 L 35,21 L 18,29 Z", "#123247");
            context.DrawEllipse(Brush("#8AF0B5"), null, new Point(22,23), 1.4, 1.4);
            context.DrawEllipse(Brush("#8AF0B5"), null, new Point(27,21), 1.4, 1.4);
            context.DrawEllipse(Brush("#8AF0B5"), null, new Point(32,19), 1.4, 1.4);
        }
        else if (Kind == "server")
        {
            // A compact isometric server cabinet.
            Shape("M 6,9 L 21,3 L 34,9 L 19,15 Z", "#447E9D");
            Shape("M 6,9 L 19,15 L 19,31 L 6,25 Z", "#274C74");
            Shape("M 19,15 L 34,9 L 34,25 L 19,31 Z", "#172F56");
            context.DrawLine(new Pen(Brush("#B9A1FF"), 1.5), new Point(21,20), new Point(31,16));
            context.DrawLine(new Pen(Brush("#B9A1FF"), 1.5), new Point(21,25), new Point(31,21));
        }
        else
        {
            // Desktop terminal, angled in isometric perspective.
            Shape("M 4,7 L 24,3 L 35,10 L 15,15 Z", "#4A94A9");
            Shape("M 4,7 L 15,15 L 15,27 L 4,19 Z", "#1B526B");
            Shape("M 15,15 L 35,10 L 35,22 L 15,27 Z", "#123448");
            Shape("M 17,16 L 32,12 L 32,20 L 17,24 Z", "#092331");
            context.DrawLine(new Pen(Brush("#8AF0B5"), 1.5), new Point(19,20), new Point(24,18));
            Shape("M 13,29 L 24,26 L 31,29 L 20,32 Z", "#377C91");
        }
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
