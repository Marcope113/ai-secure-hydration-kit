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

        if (Kind == "tux")
        {
            // Stylized Tux: original vector silhouette, independent of emoji fonts.
            context.DrawEllipse(Brush("#101B2A"), new Pen(Brush("#6CDCEA"), 1.2),
                new Point(19,19), 12, 15);
            context.DrawEllipse(Brush("#F2F5F8"), null, new Point(19,23), 7, 9);
            context.DrawEllipse(Brush("#F2F5F8"), null, new Point(15,10), 2.8, 3);
            context.DrawEllipse(Brush("#F2F5F8"), null, new Point(23,10), 2.8, 3);
            context.DrawEllipse(Brush("#172433"), null, new Point(15.5,10.5), 1.1, 1.3);
            context.DrawEllipse(Brush("#172433"), null, new Point(22.5,10.5), 1.1, 1.3);
            Shape("M 15,15 L 23,15 L 19,20 Z", "#F2B54D");
            Shape("M 7,29 L 16,29 L 15,33 L 4,33 Z", "#F2B54D");
            Shape("M 22,29 L 31,29 L 34,33 L 23,33 Z", "#F2B54D");
        }
        else if (Kind == "network")
        {
            // Isometric switch: top, front, right side and three tiny ports.
            Shape("M 4,12 L 21,5 L 35,12 L 18,20 Z", "#2B6C82");
            Shape("M 4,12 L 18,20 L 18,29 L 4,21 Z", "#174158");
            Shape("M 18,20 L 35,12 L 35,21 L 18,29 Z", "#123247");
            context.DrawEllipse(Brush("#8AF0B5"), null, new Point(22,23), 1.4, 1.4);
            context.DrawEllipse(Brush("#8AF0B5"), null, new Point(27,21), 1.4, 1.4);
            context.DrawEllipse(Brush("#8AF0B5"), null, new Point(32,19), 1.4, 1.4);
        }
        else if (Kind == "hypervisor")
        {
            Shape("M 4,11 L 19,3 L 35,11 L 20,19 Z", "#63B8D5");
            Shape("M 4,11 L 20,19 L 20,32 L 4,24 Z", "#326A9C");
            Shape("M 20,19 L 35,11 L 35,24 L 20,32 Z", "#153957");
            Shape("M 9,13 L 19,18 L 19,26 L 9,21 Z", "#102A42");
            context.DrawLine(new Pen(Brush("#A4F4FF"), 1.6), new Point(10,17), new Point(17,20));
        }
        else if (Kind == "windows")
        {
            Shape("M 5,8 L 18,5 L 18,17 L 5,18 Z", "#39B9EC");
            Shape("M 20,5 L 34,2 L 34,16 L 20,17 Z", "#1585CB");
            Shape("M 5,20 L 18,19 L 18,31 L 5,28 Z", "#168DD3");
            Shape("M 20,19 L 34,18 L 34,33 L 20,31 Z", "#1B69AA");
        }
        else if (Kind == "cpu")
        {
            Shape("M 6,10 L 24,5 L 34,13 L 16,19 Z", "#4CB3D4");
            Shape("M 6,10 L 16,19 L 16,29 L 6,20 Z", "#255C8E");
            Shape("M 16,19 L 34,13 L 34,23 L 16,29 Z", "#143958");
            Shape("M 13,11 L 23,8 L 29,13 L 19,16 Z", "#A5E8F4");
        }
        else if (Kind == "disk")
        {
            Shape("M 4,13 L 25,6 L 36,14 L 15,22 Z", "#6AA6C3");
            Shape("M 4,13 L 15,22 L 15,29 L 4,20 Z", "#315E82");
            Shape("M 15,22 L 36,14 L 36,21 L 15,29 Z", "#173E59");
            context.DrawEllipse(Brush("#9DEAF1"), null, new Point(21,14), 4, 3);
        }
        else if (Kind == "ram")
        {
            Shape("M 4,13 L 29,6 L 36,13 L 11,21 Z", "#64CEAE");
            Shape("M 4,13 L 11,21 L 11,28 L 4,20 Z", "#257E76");
            Shape("M 11,21 L 36,13 L 36,20 L 11,28 Z", "#13545D");
            for (var i = 0; i < 4; i++)
                context.DrawEllipse(Brush("#D2F7D8"), null, new Point(15 + i * 5, 17 - i), 1.5, 1.5);
        }
        else if (Kind == "folder")
        {
            Shape("M 3,12 L 16,8 L 21,12 L 35,8 L 35,26 L 3,30 Z", "#2D94B2");
            Shape("M 3,16 L 35,12 L 35,28 L 3,32 Z", "#155171");
        }
        else if (Kind == "kali-workstation")
        {
            // Workstation monitor and keyboard with cyan terminal details.
            Shape("M 3,8 L 25,3 L 36,9 L 14,15 Z", "#5DBDD3");
            Shape("M 3,8 L 14,15 L 14,28 L 3,21 Z", "#234E72");
            Shape("M 14,15 L 36,9 L 36,23 L 14,28 Z", "#16354F");
            Shape("M 17,17 L 33,13 L 33,22 L 17,26 Z", "#081B2A");
            context.DrawLine(new Pen(Brush("#86F4CE"), 1.8), new Point(19,20), new Point(23,21));
            context.DrawLine(new Pen(Brush("#86F4CE"), 1.4), new Point(25,22), new Point(30,21));
            Shape("M 11,30 L 24,27 L 35,30 L 21,33 Z", "#4D91A7");
        }
        else if (Kind == "debian-server")
        {
            // Server tower with a clearly different purple front panel.
            Shape("M 5,7 L 19,2 L 34,9 L 20,14 Z", "#A8A1D7");
            Shape("M 5,7 L 20,14 L 20,32 L 5,25 Z", "#514E8E");
            Shape("M 20,14 L 34,9 L 34,27 L 20,32 Z", "#272D58");
            context.DrawLine(new Pen(Brush("#E0C5FF"), 1.7), new Point(22,19), new Point(31,16));
            context.DrawLine(new Pen(Brush("#E0C5FF"), 1.7), new Point(22,24), new Point(31,21));
            context.DrawEllipse(Brush("#83E8BA"), null, new Point(10,15), 1.8, 1.8);
            context.DrawEllipse(Brush("#83E8BA"), null, new Point(10,21), 1.8, 1.8);
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
