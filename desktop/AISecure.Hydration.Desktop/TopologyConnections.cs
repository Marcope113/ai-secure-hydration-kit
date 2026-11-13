using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AISecure.Hydration.Desktop;

/// <summary>
/// Draws the whole lab circuit in one visual coordinate system. The lines
/// follow the actual positions of the cards after every layout pass.
/// </summary>
public sealed class TopologyConnections : Control
{
    public Control? Attacker { get; set; }
    public Control? Network { get; set; }
    public Control? Victim { get; set; }
    public Control? Hypervisor { get; set; }
    public Control? Host { get; set; }
    public bool HypervisorReady { get; set; }
    public bool Bright { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Attacker is null || Network is null || Victim is null ||
            Hypervisor is null || Host is null) return;

        Point? Bottom(Control control) =>
            control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height), this);
        Point? Top(Control control) =>
            control.TranslatePoint(new Point(control.Bounds.Width / 2, 0), this);

        var attacker = Bottom(Attacker);
        var network = Bottom(Network);
        var victim = Bottom(Victim);
        var hypervisorTop = Top(Hypervisor);
        var hypervisorBottom = Bottom(Hypervisor);
        var hostTop = Top(Host);
        if (attacker is null || network is null || victim is null ||
            hypervisorTop is null || hypervisorBottom is null || hostTop is null) return;

        var cyan = new Pen(new SolidColorBrush(Color.Parse(Bright ? "#53DCE8" : "#3B6476")), 3);
        var green = new Pen(new SolidColorBrush(Color.Parse(
            HypervisorReady ? (Bright ? "#85EAB6" : "#67D7A2")
                            : (Bright ? "#53DCE8" : "#3B6476"))), 3);

        // One continuous three-way fork, positioned between the VM row and hypervisor.
        var busY = (Math.Max(attacker.Value.Y, Math.Max(network.Value.Y, victim.Value.Y))
                    + hypervisorTop.Value.Y) / 2;
        var x1 = attacker.Value.X;
        var x2 = network.Value.X;
        var x3 = victim.Value.X;
        context.DrawLine(cyan, new Point(x1, attacker.Value.Y), new Point(x1, busY));
        context.DrawLine(cyan, new Point(x2, network.Value.Y), new Point(x2, busY));
        context.DrawLine(cyan, new Point(x3, victim.Value.Y), new Point(x3, busY));
        context.DrawLine(cyan, new Point(x1, busY), new Point(x3, busY));
        context.DrawLine(cyan, new Point(x2, busY), hypervisorTop.Value);

        // The lower link is part of the same overlay, so it cannot drift on resize.
        context.DrawLine(green, hypervisorBottom.Value, hostTop.Value);
    }
}
