using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AISecure.Hydration.Desktop;

public sealed class MainWindow : Window
{
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _host = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _target = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _attacker = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _choice = new()
    {
        ItemsSource = new[] { "Kali Linux", "Parrot Security OS" },
        SelectedIndex = 0,
        MinWidth = 240
    };
    private readonly string _stateRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AISecureAcademy");

    public MainWindow()
    {
        Title = "AI Secure Academy | Hydration Kit";
        Width = 960;
        Height = 720;
        MinWidth = 680;
        MinHeight = 560;
        Background = new SolidColorBrush(Color.Parse("#0B1520"));

        var main = new StackPanel { Spacing = 18, Margin = new Thickness(28) };
        main.Children.Add(Label("// AI SECURE A C A D E M Y", 18, "#55DCE8", FontWeight.Bold));
        main.Children.Add(Label("HYDRATION KIT: Linux PrivEsc Techniques", 24, "#F1F5F9", FontWeight.SemiBold));
        main.Children.Add(Label("Windows desktop preview  |  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), 13, "#9FB0BF"));
        main.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#304557")) });
        main.Children.Add(Label("HOST ENVIRONMENT", 15, "#55DCE8", FontWeight.Bold));
        main.Children.Add(_host);
        main.Children.Add(Label("TARGET  |  Debian 13", 15, "#C7AAFF", FontWeight.Bold));
        main.Children.Add(_target);
        main.Children.Add(Label("ATTACKER  |  Select distribution", 15, "#C7AAFF", FontWeight.Bold));
        main.Children.Add(_choice);
        main.Children.Add(_attacker);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var refresh = new Button { Content = "Refresh diagnostics", Padding = new Thickness(16, 10) };
        refresh.Click += (_, _) => Refresh();
        var save = new Button { Content = "Save attacker selection", Padding = new Thickness(16, 10) };
        save.Click += (_, _) => SaveSelection();
        buttons.Children.Add(refresh);
        buttons.Children.Add(save);
        main.Children.Add(buttons);
        main.Children.Add(Label("ACTIVITY", 15, "#55DCE8", FontWeight.Bold));
        main.Children.Add(_status);
        main.Children.Add(Label("Preview: no VM creation, OS installation or downloads are enabled.", 12, "#E8B65B"));
        Content = new ScrollViewer { Content = main };
        Refresh();
    }

    private static TextBlock Label(string value, double size, string color, FontWeight weight = FontWeight.Normal) =>
        new() { Text = value, FontSize = size, Foreground = new SolidColorBrush(Color.Parse(color)), FontWeight = weight, TextWrapping = TextWrapping.Wrap };

    private void Refresh()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024 / 1024;
            var vbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Oracle", "VirtualBox", "VBoxManage.exe");
            _host.Text = $"OS: {RuntimeInformation.OSDescription}\nArchitecture: {RuntimeInformation.OSArchitecture}\nAvailable disk: {freeGb:F1} GB\nVirtualBox: {(File.Exists(vbox) ? "Detected" : "Not detected")}\nMemory: Not checked in this preview";
            _target.Text = $"VM manifest: {Exists("hydration-v02.json")}\nDebian media manifest: {Exists("debian-media.json")}\nGuest installation: Not verified";
            var selected = ReadSelection();
            _attacker.Text = selected is null ? "Not selected / not installed" : $"Saved selection: {selected} (installation pending)";
            _choice.SelectedIndex = selected == "parrot" ? 1 : 0;
            _status.Text = "Diagnostics refreshed. No changes made.";
        }
        catch (Exception ex) { _status.Text = "Diagnostic error: " + ex.Message; }
    }

    private string Exists(string name) => File.Exists(Path.Combine(_stateRoot, name)) ? "Found" : "Not found";

    private string? ReadSelection()
    {
        var file = Path.Combine(_stateRoot, "attacker-selection.json");
        if (!File.Exists(file)) return null;
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        return document.RootElement.TryGetProperty("distribution", out var value) ? value.GetString() : null;
    }

    private void SaveSelection()
    {
        try
        {
            var distribution = _choice.SelectedIndex == 1 ? "parrot" : "kali";
            Directory.CreateDirectory(_stateRoot);
            var destination = Path.Combine(_stateRoot, "attacker-selection.json");
            if (File.Exists(destination))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(destination));
                if (document.RootElement.TryGetProperty("stage", out var stage) &&
                    stage.GetString() != "selected-not-downloaded")
                {
                    _status.Text = "Selection is already provisioned or in progress. Refusing to overwrite its manifest.";
                    return;
                }
            }
            var state = new
            {
                schemaVersion = 1,
                labId = "linux-privesc",
                role = "attacker",
                distribution,
                stage = "selected-not-downloaded",
                network = "aisecurelab",
                selectedAt = DateTimeOffset.Now.ToString("o")
            };
            var temp = destination + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, destination, true);
            _status.Text = $"Saved {distribution} preference. No ISO downloaded or VM changed.";
            Refresh();
        }
        catch (Exception ex) { _status.Text = "Could not save selection: " + ex.Message; }
    }
}
