using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AISecure.Hydration.Desktop;

public sealed class MainWindow : Window
{
    private readonly StackPanel _main = new() { Spacing = 18, Margin = new Thickness(28) };
    private readonly Button _next = new() { Content = "Next: Learning Path  >", IsEnabled = false, Padding = new Thickness(18, 10) };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _host = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _target = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _targetIp = new() { Text = "IP: Not assigned", Foreground = new SolidColorBrush(Color.Parse("#A7B8CB")) };
    private readonly TextBlock _attackerIp = new() { Text = "IP: Not assigned", Foreground = new SolidColorBrush(Color.Parse("#A7B8CB")) };
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
        Width = 1120;
        Height = 800;
        MinWidth = 680;
        MinHeight = 560;
        Background = new SolidColorBrush(Color.Parse("#0B1520"));

        var main = _main;
        main.Children.Add(Label("// AI SECURE  A C A D E M Y", 21, "#55DCE8", FontWeight.Bold));
        main.Children.Add(Label("LEARN  /  PRACTICE  /  VALIDATE", 12, "#A7B8CB"));
        main.Children.Add(Label("HYDRATION KIT: Linux PrivEsc Techniques", 24, "#F1F5F9", FontWeight.SemiBold));
        main.Children.Add(Label("Windows desktop preview  |  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), 13, "#9FB0BF"));
        main.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.Parse("#304557")) });
        main.Children.Add(Label("VIRTUAL LAB TOPOLOGY", 18, "#55DCE8", FontWeight.Bold));
        main.Children.Add(Label("Two isolated machines connected through the aisecurelab internal network.", 13, "#A7B8CB"));

        var topology = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), ColumnSpacing = 14 };
        var attackerCard = new StackPanel { Spacing = 10 };
        attackerCard.Children.Add(Label("ATTACKER", 13, "#55DCE8", FontWeight.Bold));
        attackerCard.Children.Add(Label("Kali Linux / Parrot OS", 20, "#F1F5F9", FontWeight.SemiBold));
        attackerCard.Children.Add(_attacker);
        attackerCard.Children.Add(_attackerIp);
        attackerCard.Children.Add(Label("OS version: Not detected", 13, "#A7B8CB"));
        attackerCard.Children.Add(_choice);
        attackerCard.Children.Add(InstallAction("Install Attacker", "Attacker installation is not enabled yet."));
        var attackerPanel = new Border { Background = new SolidColorBrush(Color.Parse("#172737")), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = attackerCard };
        Grid.SetColumn(attackerPanel, 0);
        topology.Children.Add(attackerPanel);

        var link = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        link.Children.Add(Label("<---->", 18, "#55DCE8", FontWeight.Bold));
        link.Children.Add(Label("ISOLATED", 11, "#A7B8CB"));
        Grid.SetColumn(link, 1);
        topology.Children.Add(link);

        var targetCard = new StackPanel { Spacing = 10 };
        targetCard.Children.Add(Label("VICTIM / TARGET", 13, "#C7AAFF", FontWeight.Bold));
        targetCard.Children.Add(Label("Debian 13", 20, "#F1F5F9", FontWeight.SemiBold));
        targetCard.Children.Add(_target);
        targetCard.Children.Add(_targetIp);
        targetCard.Children.Add(Label("ISO: Debian 13.7.0 (guest not verified)", 13, "#A7B8CB"));
        targetCard.Children.Add(InstallAction("Install Target", "Target installation is not enabled yet."));
        var targetPanel = new Border { Background = new SolidColorBrush(Color.Parse("#172737")), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = targetCard };
        Grid.SetColumn(targetPanel, 2);
        topology.Children.Add(targetPanel);
        main.Children.Add(topology);
        main.Children.Add(Label("NETWORK  /  VirtualBox internal: aisecurelab  |  No external access after provisioning", 12, "#A7B8CB"));

        main.Children.Add(Label("HOST REQUIREMENTS", 15, "#55DCE8", FontWeight.Bold));
        main.Children.Add(_host);
        main.Children.Add(RequirementCard("Windows and architecture", "Detects supported Windows host and CPU architecture.", "Windows 11 x64 is supported.", false));
        main.Children.Add(RequirementCard("VirtualBox hypervisor", "Checks the installed VirtualBox executable.", "If missing: install VirtualBox from https://www.virtualbox.org/wiki/Downloads, then restart this app. For automated Windows installation: winget install --id Oracle.VirtualBox --exact", true));
        main.Children.Add(RequirementCard("Disk space and memory", "Checks disk capacity; memory verification is planned.", "Recommended: at least 8 GB RAM and 35 GB free disk for the initial VM. Two VMs may require more.", false));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var refresh = new Button { Content = "Refresh diagnostics", Padding = new Thickness(16, 10) };
        refresh.Click += (_, _) => Refresh();
        var save = new Button { Content = "Save attacker selection", Padding = new Thickness(16, 10) };
        save.Click += (_, _) => SaveSelection();
        buttons.Children.Add(refresh);
        buttons.Children.Add(save);
        main.Children.Add(buttons);
        main.Children.Add(Label("STEP 1 OF 2  /  PREPARE YOUR LAB", 15, "#55DCE8", FontWeight.Bold));
        main.Children.Add(Label("Learning Path unlocks after the target and attacker VMs have been verified.", 13, "#A7B8CB"));
        _next.Click += (_, _) => ShowLearningPath();
        main.Children.Add(_next);
        main.Children.Add(Label("ACTIVITY", 15, "#55DCE8", FontWeight.Bold));
        main.Children.Add(_status);
        main.Children.Add(Label("Preview: installation controls are disabled until verified. Manual instructions are available for requirements.", 12, "#E8B65B"));
        Content = new ScrollViewer { Content = main };
        Refresh();
        UpdateNextState();
    }

    private void UpdateNextState()
    {
        // Do not infer readiness from manifest presence. A future health check must
        // verify guest installation, both VM states, and isolated networking.
        _next.IsEnabled = false;
        _next.Content = "Next: Learning Path  (Lab verification pending)";
    }

    private void ShowLearningPath()
    {
        if (!_next.IsEnabled) return;
        _main.Children.Clear();
        _main.Children.Add(Label("// AI SECURE  A C A D E M Y", 21, "#55DCE8", FontWeight.Bold));
        _main.Children.Add(Label("STEP 2 OF 2  /  LEARNING PATH", 15, "#55DCE8", FontWeight.Bold));
        _main.Children.Add(Label("Linux PrivEsc Techniques", 24, "#F1F5F9", FontWeight.SemiBold));
        _main.Children.Add(Label("01  Linux Fundamentals", 16, "#F2F6FC"));
        _main.Children.Add(Label("02  Sudo Misconfigurations", 16, "#F2F6FC"));
        _main.Children.Add(Label("03  SUID Exploitation", 16, "#F2F6FC"));
        _main.Children.Add(Label("04  Cron Jobs", 16, "#F2F6FC"));
        _main.Children.Add(Label("05  Linux Capabilities", 16, "#F2F6FC"));
        _main.Children.Add(Label("Lesson content and completion checks are not yet implemented.", 12, "#E8B65B"));
        var back = new Button { Content = "< Back to Lab Setup", Padding = new Thickness(18, 10) };
        back.Click += (_, _) => ShowSetup();
        _main.Children.Add(back);
    }

    private void ShowSetup()
    {
        // Reconstruct the setup view only after navigation is enabled in a future release.
        _status.Text = "Return to Lab Setup will be enabled with the validated navigation flow.";
    }

    private static TextBlock Label(string value, double size, string color, FontWeight weight = FontWeight.Normal) =>
        new() { Text = value, FontSize = size, Foreground = new SolidColorBrush(Color.Parse(color)), FontWeight = weight, TextWrapping = TextWrapping.Wrap };

    private Border RequirementCard(string name, string description, string manual, bool installable)
    {
        var inner = new StackPanel { Spacing = 8 };
        inner.Children.Add(Label(name, 16, "#F2F6FC", FontWeight.SemiBold));
        inner.Children.Add(Label(description, 13, "#A7B8CB"));
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        controls.Children.Add(new Button { Content = "Install (not available)", IsEnabled = false });
        var help = new Button { Content = "Manual instructions" };
        help.Click += (_, _) => _status.Text = name + ": " + manual;
        controls.Children.Add(help);
        inner.Children.Add(controls);
        return new Border { Background = new SolidColorBrush(Color.Parse("#172737")), CornerRadius = new CornerRadius(10), Padding = new Thickness(16), Child = inner };
    }

    private Button InstallAction(string label, string message)
    {
        var button = new Button { Content = label + " (not available)", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
        return button;
    }

    private void Refresh()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024 / 1024;
            var vbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Oracle", "VirtualBox", "VBoxManage.exe");
            var osName = OperatingSystem.IsWindows() && Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : RuntimeInformation.OSDescription;
            _host.Text = $"OS: {osName}\nArchitecture: {RuntimeInformation.OSArchitecture}\nAvailable disk: {freeGb:F1} GB\nVirtualBox: {(File.Exists(vbox) ? "Detected" : "Not detected")}\nMemory: Not checked in this preview";
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
