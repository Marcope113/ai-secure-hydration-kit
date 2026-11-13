using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AISecure.Hydration.Desktop;

public sealed class MainWindow : Window
{
    private const string Accent = "#53DCE8", Purple = "#C5A4FF", Text = "#F0F5FA", Muted = "#A3B5C7";
    private const string Green = "#67D7A2", Yellow = "#F0BF67", Red = "#FF7F8D";
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AISecureAcademy");
    private readonly StackPanel _page = new() { Spacing = 18, Margin = new Thickness(30, 26) };
    private readonly ComboBox _distro = new() { ItemsSource = new[] { "Kali Linux", "Parrot Security OS" }, SelectedIndex = 0, MinWidth = 210 };
    private readonly TextBlock _activity = Txt("Waiting for diagnostics.", 13, Muted);
    private readonly Button _next = new() { Content = "Next: Learning Path (verification pending)", IsEnabled = false, Padding = new Thickness(18, 12) };
    private string _selected = "kali";
    private readonly TextBlock _attackerOs = Txt("Kali Linux", 22, Text, FontWeight.SemiBold);


    public MainWindow()
    {
        Title = "AI Secure Academy | Hydration Kit";
        Width = 1160; Height = 840; MinWidth = 800; MinHeight = 580;
        Background = Paint("#0B1520");
        Content = new ScrollViewer { Content = _page };
        _next.Click += (_, _) => ShowLessons();
        var saved = ReadSelection();
        _selected = saved == "parrot" ? "parrot" : "kali";
        _distro.SelectedIndex = _selected == "parrot" ? 1 : 0;
        _distro.SelectionChanged += (_, _) => UpdateAttackerSelection();
        ShowSetup();
    }

    private void UpdateAttackerSelection()
    {
        _selected = _distro.SelectedIndex == 1 ? "parrot" : "kali";
        _attackerOs.Text = _selected == "parrot" ? "Parrot Security OS" : "Kali Linux";

    }

    private static IBrush Paint(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static TextBlock Txt(string value, double size = 14, string color = Text, FontWeight weight = FontWeight.Normal) =>
        new() { Text = value, FontSize = size, Foreground = Paint(color), FontWeight = weight, TextWrapping = TextWrapping.Wrap };
    private static Border Tile(Control child, string background = "#172737") =>
        new() { Background = Paint(background), CornerRadius = new CornerRadius(12), Padding = new Thickness(19), Child = child };
    private static StackPanel Stack(int gap = 9) => new() { Spacing = gap };
    private static Button Button(string title, Action click)
    {
        var b = new Button { Content = title, Padding = new Thickness(14, 9), HorizontalAlignment = HorizontalAlignment.Left };
        b.Click += (_, _) => click();
        return b;
    }
    private static string State(bool ok) => ok ? "SUPPORTED" : "NOT SUPPORTED";
    private string Existing(string name) => File.Exists(Path.Combine(_root, name)) ? "Found" : "Not found";

    private void Header(string section)
    {
        _page.Children.Clear();
        _page.Children.Add(Txt("// AI SECURE  A C A D E M Y", 21, Accent, FontWeight.Bold));
        _page.Children.Add(Txt(section, 12, Muted, FontWeight.SemiBold));
    }
    private void Section(string title) => _page.Children.Add(Txt(title, 16, Accent, FontWeight.Bold));

    private Border Machine(string symbol, string role, string name, string state, string color, string details, Control? action = null, Control? titleOverride = null)
    {
        var body = Stack(9);
        body.Children.Add(Txt(symbol + "   " + role, 15, color, FontWeight.Bold));
        body.Children.Add(titleOverride ?? Txt(name, 22, Text, FontWeight.SemiBold));
        body.Children.Add(Txt(state, 13, color, FontWeight.Bold));
        body.Children.Add(Txt(details, 13, Muted));
        if (action is not null) body.Children.Add(action);
        return Tile(body);
    }

    private void ShowSetup()
    {
        Header("LAB SETUP  /  STEP 1 OF 2");
        var overview = Stack(8);
        overview.Children.Add(Txt("Linux PrivEsc Techniques", 28, Text, FontWeight.Bold));
        overview.Children.Add(Txt("Explore how Linux privilege boundaries fail. Enumerate misconfigurations, gain elevated access in an isolated environment, then investigate detection and remediation.", 14, Muted));
        overview.Children.Add(Txt("One target Debian VM + your choice of Kali Linux or Parrot Security OS.", 13, Accent));
        _page.Children.Add(Tile(overview));

        Section("VIRTUAL LAB TOPOLOGY");
        var network = new Grid { ColumnDefinitions = new ColumnDefinitions("*,150,*"), ColumnSpacing = 12 };
        var attackerControls = Stack(8);
        
        attackerControls.Children.Add(_distro);
        attackerControls.Children.Add(Button("Save selection", SaveSelection));
        var attacker = Machine("[>_]", "ATTACKER", _selected == "parrot" ? "Parrot Security OS" : "Kali Linux",
            "NOT INSTALLED", Yellow, "PLANNED IP   10.4.4.10\nOS VERSION   Not installed", attackerControls, _attackerOs);
        Grid.SetColumn(attacker, 0); network.Children.Add(attacker);
        var link = Stack(8);
        link.VerticalAlignment = VerticalAlignment.Center;
        link.HorizontalAlignment = HorizontalAlignment.Stretch;
        var networkName = Txt("aisecurelab", 13, Text, FontWeight.SemiBold);
        networkName.TextAlignment = TextAlignment.Center;
        var networkType = Txt("INTERNAL NETWORK", 10, Muted);
        networkType.TextAlignment = TextAlignment.Center;
        var networkArrow = Txt("<------>", 20, Accent, FontWeight.Bold);
        networkArrow.TextAlignment = TextAlignment.Center;
        link.Children.Add(networkArrow);
        link.Children.Add(networkName);
        link.Children.Add(networkType);
        var linkTile = new Border {
            Background = Paint("#122432"), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 14), VerticalAlignment = VerticalAlignment.Center,
            Child = link
        };
        Grid.SetColumn(linkTile, 1); network.Children.Add(linkTile);
        var target = Machine("[#]", "VICTIM / TARGET", "Debian 13",
            Existing("debian-media.json") == "Found" ? "ISO ATTACHED" : "NOT PREPARED", Purple,
            "PLANNED IP   10.4.4.20\nOS VERSION   Debian 13 (not installed)\nMEDIA   Debian 13.7.0 ISO");
        Grid.SetColumn(target, 2); network.Children.Add(target);
        _page.Children.Add(network);
        _page.Children.Add(Txt("IPs are planned static addresses (10.4.4.0/24), not yet configured or verified in either guest.", 12, Muted));

        Section("TECHNIQUES COVERED");
        var techniques = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10, RowDefinitions = new RowDefinitions("Auto,Auto"), RowSpacing = 10 };
        var items = new (string,string)[] {
            ("01  Linux Enumeration", "Users, processes and permissions"),
            ("02  Sudo Misconfigurations", "Unsafe delegated privileges"),
            ("03  SUID Binaries", "Privileged executable paths"),
            ("04  Cron Jobs", "Scheduled task weaknesses"),
            ("05  Linux Capabilities", "Granular privilege misuse"),
            ("06  PATH & Permissions", "Environment and file trust")
        };
        for (var i = 0; i < items.Length; i++)
        {
            var body = Stack(6);
            body.Children.Add(Txt(items[i].Item1, 14, Text, FontWeight.SemiBold));
            body.Children.Add(Txt(items[i].Item2, 12, Muted));
            var tile = Tile(body);
            Grid.SetColumn(tile, i % 3); Grid.SetRow(tile, i / 3); techniques.Children.Add(tile);
        }
        _page.Children.Add(techniques);

        Section("HOST REQUIREMENTS");
        var req = Stack(12);
        var build = Environment.OSVersion.Version.Build;
        var windows = OperatingSystem.IsWindows();
        var architecture = RuntimeInformation.OSArchitecture;
        var archOk = windows && architecture == Architecture.X64 && build >= 22000;
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var free = drive.AvailableFreeSpace / 1073741824d;
        var diskOk = free >= 35;
        var memoryGb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824d;
        // TotalAvailableMemoryBytes is a process limit, not physical RAM. Do not report it as host memory.
        var vboxPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Oracle", "VirtualBox", "VBoxManage.exe");
        var vboxOk = File.Exists(vboxPath);

        var summary = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 12 };
        var architectureTile = Requirement("ARCHITECTURE", architecture.ToString(), State(archOk), archOk ? Green : Red,
            "Windows 11 x64 is required for this Windows preview.", null);
        var diskTile = Requirement("DISK SPACE", $"{free:F1} GB free", diskOk ? "SUPPORTED" : "NOT ENOUGH SPACE", diskOk ? Green : Red,
            "Minimum 35 GB free for the initial target VM. More space is recommended for two VMs.", null);
        var vboxTile = Requirement("VIRTUALBOX", vboxOk ? "Detected" : "Missing", vboxOk ? "SUPPORTED" : "INSTALL REQUIRED", vboxOk ? Green : Red,
            "Manual installation: https://www.virtualbox.org/wiki/Downloads", vboxOk ? null : "Install VirtualBox");
        Grid.SetColumn(architectureTile, 0); summary.Children.Add(architectureTile);
        Grid.SetColumn(diskTile, 1); summary.Children.Add(diskTile);
        Grid.SetColumn(vboxTile, 2); summary.Children.Add(vboxTile);
        req.Children.Add(summary);
        req.Children.Add(Txt("Host: " + (windows && build >= 22000 ? "Windows 11" : RuntimeInformation.OSDescription) +
            "  |  Physical RAM: verification pending  |  Virtualization: verification pending", 12, Muted));
        _page.Children.Add(req);

        Section("LAB INSTALLATION");
        var setup = Stack(8);
        setup.Children.Add(Txt("Target: Debian media prepared, guest installation not verified. Attacker: distribution selection only.", 13, Muted));
        setup.Children.Add(Txt("Installation controls will be enabled after provisioning operations and rollback behavior pass integration tests.", 12, Yellow));
        _page.Children.Add(Tile(setup));
        _page.Children.Add(Button("Refresh diagnostics", ShowSetup));
        _page.Children.Add(Txt("NEXT STEP", 13, Accent, FontWeight.Bold));
        _next.IsEnabled = false;
        _page.Children.Add(_next);
        Section("ACTIVITY");
        _page.Children.Add(_activity);
    }

    private Border Requirement(string title, string detail, string status, string statusColor, string manual, string? installLabel)
    {
        var body = Stack(8);
        body.Children.Add(Txt(title, 12, Muted, FontWeight.Bold));
        body.Children.Add(Txt(detail, 18, Text, FontWeight.SemiBold));
        body.Children.Add(Txt(status, 13, statusColor, FontWeight.Bold));
        if (installLabel is not null)
        {
            // No provisioning operation is wired yet: never show a misleading active install button.
            body.Children.Add(Txt("Automatic install: coming after validation", 11, Yellow));
            body.Children.Add(Button("Manual instructions", () => ShowInstructions(title, manual)));
        }
        return Tile(body);
    }

    private void ShowInstructions(string title, string instructions)
    {
        _activity.Text = title + ": " + instructions;
        var dialog = new Window
        {
            Title = "Manual instructions - " + title, Width = 540, Height = 220,
            Background = Paint("#152332"),
            Content = new TextBlock { Text = instructions, Foreground = Paint(Text), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) }
        };
        dialog.Show(this);
    }

    private string? ReadSelection()
    {
        var file = Path.Combine(_root, "attacker-selection.json");
        if (!File.Exists(file)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        return doc.RootElement.TryGetProperty("distribution", out var val) ? val.GetString() : null;
    }

    private void SaveSelection()
    {
        try
        {
            var selection = _distro.SelectedIndex == 1 ? "parrot" : "kali";
            Directory.CreateDirectory(_root);
            var file = Path.Combine(_root, "attacker-selection.json");
            if (File.Exists(file))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.TryGetProperty("stage", out var stage) && stage.GetString() != "selected-not-downloaded")
                {
                    _activity.Text = "Existing installation state cannot be overwritten."; return;
                }
            }
            var state = new { schemaVersion = 1, labId = "linux-privesc", role = "attacker",
                distribution = selection, stage = "selected-not-downloaded", network = "aisecurelab",
                selectedAt = DateTimeOffset.Now.ToString("o") };
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, file, true);
            _selected = selection;
            UpdateAttackerSelection();
            _activity.Text = "Attacker selection saved. No VM changes made.";
            ShowSetup();
        }
        catch (Exception ex) { _activity.Text = "Selection error: " + ex.Message; }
    }

    private void ShowLessons()
    {
        if (!_next.IsEnabled) return;
        Header("LEARNING PATH  /  STEP 2 OF 2");
        _page.Children.Add(Txt("Linux PrivEsc Techniques", 28, Text, FontWeight.Bold));
        _page.Children.Add(Txt("Lessons will be unlocked after both virtual machines and their isolated network pass validation.", 14, Muted));
        _page.Children.Add(Button("Back to Lab Setup", ShowSetup));
    }
}
