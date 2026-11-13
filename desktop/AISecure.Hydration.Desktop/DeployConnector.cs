using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AISecure.Hydration.Desktop;

/// <summary>One unbroken line aligned to the actual deploy button and milestone centers.</summary>
public sealed class DeployConnector : Control
{
    public Control? StartButton { get; set; }
    public Control? FinalStage { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (StartButton is null || FinalStage is null) return;
        var start = StartButton.TranslatePoint(
            new Point(StartButton.Bounds.Width, StartButton.Bounds.Height / 2), this);
        var end = FinalStage.TranslatePoint(
            new Point(FinalStage.Bounds.Width / 2, FinalStage.Bounds.Height / 2), this);
        if (start is null || end is null) return;
        // One geometry, no separately positioned segments or layout offsets.
        context.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#4DA5B5")), 2),
            start.Value, end.Value);
    }
}
