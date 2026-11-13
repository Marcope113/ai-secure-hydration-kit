using System.Security.Cryptography;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace AISecure.Hydration.Desktop;

public sealed class MainWindow : Window
{
    private const string Accent = "#53DCE8", Purple = "#C5A4FF", Text = "#F0F5FA", Muted = "#A3B5C7";
    private const string Green = "#67D7A2", Yellow = "#F0BF67", Red = "#FF7F8D";
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AISecureAcademy");
    private readonly StackPanel _page = new() { Spacing = 10, Margin = new Thickness(20, 14) };
    private ComboBox _distro = new() { ItemsSource = new[] { "Kali Linux", "Parrot Security OS" }, SelectedIndex = 0, MinWidth = 210 };
    private TextBlock _activity = Txt("Waiting for diagnostics.", 13, Muted);
    private Button _next = new() { Content = "Next: Learning Path (verification pending)", IsEnabled = false, Padding = new Thickness(18, 12) };
    private string _selected = "kali";
    private bool _installingVirtualBox;
    private bool _removingVirtualBox;
    private bool _downloadingKali;
    private bool _downloadingDebian;
    private double _kaliPercent;
    private double _debianPercent;
    private Button? _kaliDownloadButton;
    private Button? _debianDownloadButton;
    private bool _installingAttacker;
    private bool _kaliConfigured;
    private bool _kaliRunning;
    private bool _checkingKaliRuntime;
    private TextBlock? _kaliRuntimeLabel;
    private Border? _kaliOuterTile;
    private int _runtimePollTicks;
    private Button? _installAttackerButton;
    private string? _verifiedKaliPath;
    private string? _installedKaliFolder;
    private Button? _extractedKaliButton;
    private bool _kaliVerified;
    private bool _debianVerified;
    private string? _debianSha256;
    private string? _kaliSha256;
    private TextBlock? _kaliHashLabel;
    private TextBlock? _debianHashLabel;
    private Button? _attackerCopyButton;
    private Button? _targetCopyButton;
    private string _kaliStatus = "Waiting to download";
    private string _debianStatus = "Waiting to download";
    private DispatcherTimer? _circuitTimer;
    private readonly List<(Border segment, bool green)> _pulseSegments = new();
    private bool _pulseBright;
    private readonly string _isoRoot = ResolveIsoDirectory();
    private TextBlock _attackerProgressText = Txt("Waiting to download", 11, Muted);
    private TextBlock _targetProgressText = Txt("Waiting to download", 11, Muted);
    private ProgressBar _attackerProgress = new() { Minimum = 0, Maximum = 100, Value = 0, Height = 7 };
    private ProgressBar _targetProgress = new() { Minimum = 0, Maximum = 100, Value = 0, Height = 7 };
    private TextBlock _attackerOs = Txt("Kali Linux", 22, Text, FontWeight.SemiBold);


    public MainWindow()
    {
        Title = "AI Secure Academy | Hydration Kit";
        Width = 1160; Height = 840; MinWidth = 800; MinHeight = 580;
        Background = Paint("#0B1520");
        Content = new ScrollViewer { Content = _page };
        var saved = ReadSelection();
        _selected = "kali";
        _distro.SelectedIndex = 0;
        ShowSetup();
        Dispatcher.UIThread.Post(() => { _ = DetectRegisteredKaliAsync(); _ = RefreshKaliRuntimeAsync(); });
        // Revalidate previously downloaded images when the application starts.
        // The installer must not depend on the download having occurred in this session.
        Dispatcher.UIThread.Post(() => {
            if (Directory.Exists(_isoRoot) &&
                Directory.GetFiles(_isoRoot, "kali-linux-*-virtualbox-amd64.7z").Length > 0)
                _ = DownloadLabImageAsync("kali");
        });
    }

    private void UpdateAttackerSelection()
    {
        _selected = _distro.SelectedIndex == 1 ? "parrot" : "kali";
        _attackerOs.Text = _selected == "parrot" ? "Parrot Security OS" : "Kali Linux";

    }

    // Future provisioning events update only the corresponding tile.
    // These are intentionally idle until installation operations are implemented.
    private void SetInstallationProgress(string component, string step, double? percent)
    {
        var (bar, label) = component switch
        {
            "Attacker" => (_attackerProgress, _attackerProgressText),
            "Target" => (_targetProgress, _targetProgressText),
            _ => throw new ArgumentException("Unknown component", nameof(component))
        };
        label.Text = step;
        bar.IsIndeterminate = percent is null;
        if (percent is not null) bar.Value = Math.Clamp(percent.Value, 0, 100);
    }

    private static IBrush Paint(string hex) => new SolidColorBrush(Color.Parse(hex));
    private static TextBlock Txt(string value, double size = 14, string color = Text, FontWeight weight = FontWeight.Normal) =>
        new() { Text = value, FontSize = size, Foreground = Paint(color), FontWeight = weight, TextWrapping = TextWrapping.Wrap };
    private static Border Tile(Control child, string background = "#24394D") =>
        new() {
            Background = Paint(background), CornerRadius = new CornerRadius(12),
            Padding = new Thickness(19), Child = child,
            BorderBrush = Paint("#355368"), BorderThickness = new Thickness(1)
        };

    private static void SetReadyOutline(Border tile, bool ready)
    {
        tile.BorderBrush = Paint(ready ? "#69DFA7" : "#355368");
        tile.BorderThickness = new Thickness(ready ? 2 : 1);
    }
    private static StackPanel Stack(int gap = 9) => new() { Spacing = gap };
    private static Button Button(string title, Action click)
    {
        var b = new Button {
            Content = title, Padding = new Thickness(14, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            CornerRadius = new CornerRadius(9),
            Background = Paint("#142B42"),
            Foreground = Paint(Text),
            BorderBrush = Paint(Accent),
            BorderThickness = new Thickness(1.5),
            FontWeight = FontWeight.SemiBold
        };
        b.Click += (_, _) => click();
        return b;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    private static double? GetPhysicalRamGiB()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            return GlobalMemoryStatusEx(ref status)
                ? status.TotalPhysical / 1073741824d : null;
        }
        catch { return null; }
    }

    private int ReadVmRam(string role, int fallback)
    {
        try
        {
            var file = Path.Combine(_root, "vm-resources.json");
            if (!File.Exists(file)) return fallback;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.TryGetProperty(role, out var ram)
                && ram.TryGetInt32(out var value) && value >= 1024 && value <= 65536 && value % 1024 == 0
                ? value : fallback;
        }
        catch { return fallback; }
    }

    private void SaveVmRam(string role, int ram)
    {
        try
        {
            Directory.CreateDirectory(_root);
            var attacker = role == "attacker" ? ram : ReadVmRam("attacker", 4096);
            var target = role == "target" ? ram : ReadVmRam("target", 2048);
            File.WriteAllText(Path.Combine(_root, "vm-resources.json"),
                JsonSerializer.Serialize(new { attacker, target }));
            _activity.Text = $"Saved planned VM RAM: Kali {attacker / 1024} GB, Debian {target / 1024} GB. Not applied to VirtualBox yet.";
        }
        catch (Exception ex) { _activity.Text = "Could not save VM RAM: " + ex.Message; }
    }

    private int ReadVmResource(string role, string resource, int fallback)
    {
        try
        {
            var path = Path.Combine(_root, "vm-settings.json");
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty(role, out var vm)
                    && vm.TryGetProperty(resource, out var number)
                    && number.TryGetInt32(out var value)) return value;
            }
        }
        catch { /* Invalid settings fall back to defaults. */ }
        return resource == "memory" ? ReadVmRam(role, fallback * 1024) / 1024 : fallback;
    }

    private void SaveVmResource(string role, string resource, int value)
    {
        try
        {
            Directory.CreateDirectory(_root);
            var settings = new Dictionary<string, Dictionary<string, int>>();
            var path = Path.Combine(_root, "vm-settings.json");
            if (File.Exists(path))
            {
                try
                {
                    settings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(path))
                        ?? settings;
                }
                catch { /* Recover from malformed settings. */ }
            }
            if (!settings.ContainsKey(role)) settings[role] = new Dictionary<string, int>();
            settings[role][resource] = value;
            File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            _activity.Text = $"Saved planned {role} {resource}: {value}. Not yet applied to VirtualBox.";
        }
        catch (Exception ex) { _activity.Text = "Could not save VM resources: " + ex.Message; }
    }

    private Control VmResourceControls(string role)
    {
        var panel = Stack(1);
        panel.Width = 195;
        panel.Children.Add(Txt("VM RESOURCES", 11, Accent, FontWeight.Bold));
        // Course lab limits: deliberately small, predictable VM allocations.
        const int maxRam = 8;
        const int cpuMax = 4;
        const int diskMax = 50;

        void AddResource(string name, string key, int minimum, int maximum, int fallback, string unit)
        {
            var current = Math.Clamp(ReadVmResource(role, key, fallback), minimum, maximum);
            var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var label = Txt(name, 11, Muted, FontWeight.SemiBold);
            var value = Txt($"{current} {unit}".TrimEnd(), 11, Accent, FontWeight.Bold);
            Grid.SetColumn(value, 1);
            heading.Children.Add(label);
            heading.Children.Add(value);
            panel.Children.Add(heading);
            var slider = new Slider {
                Minimum = minimum, Maximum = maximum, TickFrequency = 1,
                IsSnapToTickEnabled = true, Value = current,
                Foreground = Paint(Accent), Background = Paint("#294153"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            slider.PropertyChanged += (_, args) =>
            {
                if (args.Property != Slider.ValueProperty) return;
                var selected = (int)Math.Round(slider.Value);
                value.Text = $"{selected} {unit}".TrimEnd();
                SaveVmResource(role, key, selected);
            };
            panel.Children.Add(slider);
        }

        if (role == "attacker")
        {
            var diskHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            diskHeading.Children.Add(Txt("DISK (DEFAULT KALI IMAGE)", 10, Muted, FontWeight.SemiBold));
            var diskValue = Txt("80 GB", 11, Accent, FontWeight.Bold);
            Grid.SetColumn(diskValue, 1);
            diskHeading.Children.Add(diskValue);
            panel.Children.Add(diskHeading);
            panel.Children.Add(new Slider {
                Minimum = 0, Maximum = 80, Value = 80,
                IsEnabled = false,
                Foreground = Paint(Accent), Background = Paint("#294153"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            });
        }
        else AddResource("DISK", "disk", 20, diskMax, 20, "GB");
        AddResource("MEMORY", "memory", 1, maxRam, 2, "GB");
        AddResource("PROCESSORS", "processors", 1, cpuMax, 2, "");
        return panel;
    }

    private static string ResolveIsoDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("AISECURE_KIT_ROOT");
        var root = !string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine(root, "downloads", "isos");
    }

    private void OpenStoragePath(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { _activity.Text = "Cannot open folder: " + ex.Message; }
    }

    private void OpenIsoFolder()
    {
        try
        {
            Directory.CreateDirectory(_isoRoot);
            Process.Start(new ProcessStartInfo(_isoRoot) { UseShellExecute = true });
        }
        catch (Exception ex) { _activity.Text = "Cannot open ISO directory: " + ex.Message; }
    }

    private static string State(bool ok) => ok ? "SUPPORTED" : "NOT SUPPORTED";
    private string Existing(string name) => File.Exists(Path.Combine(_root, name)) ? "Found" : "Not found";

    private void RegisterPulse(Border segment, bool green = false)
    {
        _pulseSegments.Add((segment, green));
        segment.Background = Paint(green ? Green : "#3B6476");
    }

    private void UpdateCircuitPulse()
    {
        _pulseBright = !_pulseBright;
        foreach (var (segment, green) in _pulseSegments)
            segment.Background = Paint(green
                ? (_pulseBright ? "#85EAB6" : Green)
                : (_pulseBright ? "#53DCE8" : "#3B6476"));
    }

    private Control CircuitConnector(bool hostReady)
    {
        var connector = Stack(0);
        connector.HorizontalAlignment = HorizontalAlignment.Stretch;
        var stem = new Border {
            Width = 4, Height = 18, HorizontalAlignment = HorizontalAlignment.Center
        };
        var node = new Border {
            Width = 9, Height = 9, CornerRadius = new CornerRadius(5),
            BorderBrush = Paint("#0F2030"), BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        connector.Children.Add(stem);
        connector.Children.Add(node);
        RegisterPulse(stem, hostReady);
        RegisterPulse(node, hostReady);
        return connector;
    }

    private Control ForkConnector()
    {
        // Responsive fork: endpoints follow the centers of the three columns.
        var fork = new Canvas { Height = 25, HorizontalAlignment = HorizontalAlignment.Stretch };

        Border Segment(double width, double height)
        {
            var segment = new Border {
                Width = width, Height = height, Background = Paint("#59788C"),
                CornerRadius = new CornerRadius(1)
            };
            fork.Children.Add(segment);
            RegisterPulse(segment);
            return segment;
        }
        var left = Segment(4, 12);
        var center = Segment(4, 12);
        var right = Segment(4, 12);
        var bus = Segment(4, 4);
        var trunk = Segment(4, 10);
        fork.SizeChanged += (_, _) =>
        {
            var width = fork.Bounds.Width;
            if (width <= 0) return;
            // Column centers for 35% / 30% / 35%, accounting for 8px gaps.
            var usable = width - 16;
            var x1 = usable * .18;
            var x2 = 8 + usable * .50;
            var x3 = 16 + usable * .82;
            Canvas.SetLeft(left, x1 - 2); Canvas.SetTop(left, 0);
            Canvas.SetLeft(center, x2 - 2); Canvas.SetTop(center, 0);
            Canvas.SetLeft(right, x3 - 2); Canvas.SetTop(right, 0);
            Canvas.SetLeft(bus, x1 - 2); Canvas.SetTop(bus, 10);
            bus.Width = x3 - x1 + 4;
            Canvas.SetLeft(trunk, x2 - 2); Canvas.SetTop(trunk, 14);
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        timer.Tick += (_, _) => UpdateCircuitPulse();
        _circuitTimer = timer;
        timer.Start();
        return fork;
    }

    private void Header(string section)
    {
        _page.Children.Clear();
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var mark = new Border {
            Width = 43, Height = 43, CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(2), BorderBrush = Paint(Accent),
            Background = Paint("#143545"),
            Child = new TextBlock {
                Text = "A✦", FontSize = 19, FontWeight = FontWeight.Bold,
                Foreground = Paint(Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        brand.Children.Add(mark);
        var brandName = Txt("AI SECURE  A C A D E M Y", 21, Accent, FontWeight.Bold);
        brandName.VerticalAlignment = VerticalAlignment.Center;
        brand.Children.Add(brandName);
        _page.Children.Add(brand);
        _page.Children.Add(Txt(section, 12, Muted, FontWeight.SemiBold));
    }
    private void Section(string title) => _page.Children.Add(Txt(title, 16, Accent, FontWeight.Bold));

    private static Control VmDetails(string details)
    {
        var rows = Stack(2);
        foreach (var line in details.Split('\n'))
        {
            var separator = line.IndexOf("   ", StringComparison.Ordinal);
            if (separator < 0) { rows.Children.Add(Txt(line, 13, Muted)); continue; }
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
            row.Children.Add(Txt(line[..separator], 13, Accent, FontWeight.Bold));
            row.Children.Add(Txt(line[(separator + 3)..].Trim(), 13, Muted));
            rows.Children.Add(row);
        }
        return rows;
    }

    private Border Machine(string symbol, string role, string name, string state, string color, string details, Control? action = null, Control? titleOverride = null)
    {
        var body = Stack(4);
        var roleHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        roleHeader.Children.Add(new IsometricLabIcon { Kind = "debian-server" });
        var roleLabel = Txt(role, 15, color, FontWeight.Bold);
        roleLabel.VerticalAlignment = VerticalAlignment.Center;
        roleHeader.Children.Add(roleLabel);
        body.Children.Add(roleHeader);
        body.Children.Add(titleOverride ?? Txt(name, 22, Text, FontWeight.SemiBold));
        body.Children.Add(Txt(state, 13, color, FontWeight.Bold));
        body.Children.Add(VmDetails(details));
        if (action is not null) body.Children.Add(action);
        // Inner VM content should not have its own outline; the outer VM tile owns it.
        return new Border { Background = Brushes.Transparent, Padding = new Thickness(19), Child = body };
    }

    private void ShowSetup()
    {
        _circuitTimer?.Stop();
        _circuitTimer = null;
        _pulseSegments.Clear();
        _pulseBright = false;
        Dispatcher.UIThread.Post(() => _ = DetectRegisteredKaliAsync());
        // Progress is model state; controls belong to one page instance only.
        // Never reparent an existing TextBlock or ProgressBar during Refresh.
        _attackerProgressText = Txt(_kaliStatus, 11, Muted);
        _targetProgressText = Txt(_debianStatus, 11, Muted);
        _attackerProgress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 7 };
        _targetProgress = new ProgressBar { Minimum = 0, Maximum = 100, Value = _debianPercent, Height = 7 };
        _attackerProgress.Value = _kaliPercent;
        // Create fresh controls for each page build. Avalonia controls cannot
        // be attached to multiple logical parents after a rebuild.
        _distro = new ComboBox { ItemsSource = new[] { "Kali Linux" }, SelectedIndex = 0, MinWidth = 210 };
        _attackerOs = Txt("Kali Linux", 22, Text, FontWeight.SemiBold);
        _activity = Txt("Ready. No VM changes made.", 13, Muted);
        _next = new Button { Content = "Next: Learning Path (verification pending)", IsEnabled = false, Padding = new Thickness(18, 12) };
        _next.Click += (_, _) => ShowLessons();

        Header("LAB SETUP  /  STEP 1 OF 2");
        var overview = Stack(2);
        var overviewHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        var tux = new IsometricLabIcon { Kind = "tux", Width = 43, Height = 40 };
        tux.VerticalAlignment = VerticalAlignment.Center;
        overviewHeader.Children.Add(tux);
        var overviewHeading = Txt("Linux PrivEsc Techniques", 23, Text, FontWeight.Bold);
        overviewHeading.VerticalAlignment = VerticalAlignment.Center;
        overviewHeader.Children.Add(overviewHeading);
        var overviewTop = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        overviewTop.Children.Add(overviewHeader);
        var refreshDiagnostics = Button("Refresh diagnostics", ShowSetup);
        refreshDiagnostics.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(refreshDiagnostics, 1);
        overviewTop.Children.Add(refreshDiagnostics);
        overview.Children.Add(overviewTop);
        overview.Children.Add(Txt("Explore how Linux privilege boundaries fail. Enumerate misconfigurations, gain elevated access in an isolated environment, then investigate detection and remediation.", 14, Muted));
        overview.Children.Add(Txt("Kali Linux attacker + Academy Debian vulnerable target, both on VirtualBox.", 13, Accent));
        overview.Children.Add(Txt("TECHNIQUES COVERED", 13, Accent, FontWeight.Bold));
        var techniques = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*"), ColumnSpacing = 7, RowDefinitions = new RowDefinitions("Auto") };
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
            var body = Stack(5);
            var techniqueTitle = Txt(items[i].Item1, 14, Text, FontWeight.SemiBold);
            techniqueTitle.TextAlignment = TextAlignment.Center;
            techniqueTitle.HorizontalAlignment = HorizontalAlignment.Stretch;
            body.Children.Add(techniqueTitle);
            var techniqueDescription = Txt(items[i].Item2, 12, Muted);
            techniqueDescription.TextAlignment = TextAlignment.Center;
            techniqueDescription.HorizontalAlignment = HorizontalAlignment.Stretch;
            body.Children.Add(techniqueDescription);
            var tile = Tile(body);
            tile.Padding = new Thickness(9, 13);
            Grid.SetColumn(tile, i); techniques.Children.Add(tile);
        }
        overview.Children.Add(techniques);



        var overviewTile = Tile(overview);
        overviewTile.Padding = new Thickness(12);
        _page.Children.Add(overviewTile);

        Section("VIRTUAL LAB ARCHITECTURE");
        var topology = Stack(2);
        topology.Children.Add(Txt("Isolated Linux privilege escalation environment", 13, Muted));
        var layers = Stack(18);
        Control WithLayer(Control content, string layer, string description)
        {
            var frame = new Grid();
            frame.Children.Add(content);
            var labels = Stack(3);
            labels.HorizontalAlignment = layer == "LAYER 03" ? HorizontalAlignment.Center : HorizontalAlignment.Right;
            labels.VerticalAlignment = layer == "LAYER 03" ? VerticalAlignment.Top : VerticalAlignment.Top;
            labels.Margin = new Thickness(0, 5, 9, 0);
            labels.IsHitTestVisible = false;
            foreach (var line in new[] { layer, description })
            {
                var text = Txt(line, line == layer ? 11 : 10, Muted,
                    line == layer ? FontWeight.Bold : FontWeight.Normal);
                text.TextAlignment = layer == "LAYER 03" ? TextAlignment.Center : TextAlignment.Right;
                text.Opacity = line == layer ? 0.65 : 0.55;
                labels.Children.Add(text);
            }
            frame.Children.Add(labels);
            return frame;
        }
        var hypervisorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Oracle", "VirtualBox", "VBoxManage.exe");
        var hypervisorReady = File.Exists(hypervisorPath);
        var network = new Grid { ColumnDefinitions = new ColumnDefinitions("36*,28*,36*"), ColumnSpacing = 6 };
        var attackerControls = Stack(2);
        // Re-discover completed extraction after app restart or diagnostics refresh.
        // Only expose folders with both VM configuration and a virtual disk.
        var kaliExtractionRoot = Path.Combine(_root, "virtual-machines", "kali");
        _installedKaliFolder = Directory.Exists(kaliExtractionRoot)
            ? Directory.GetFiles(kaliExtractionRoot, "*.vbox", SearchOption.AllDirectories)
                .Select(Path.GetDirectoryName)
                .Where(folder => folder is not null &&
                    Directory.GetFiles(folder, "*.vdi", SearchOption.AllDirectories).Length > 0)
                .OrderByDescending(folder => folder, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;
        var attackerActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var localKaliArchiveExists = Directory.Exists(_isoRoot) &&
            Directory.GetFiles(_isoRoot, "kali-linux-*-virtualbox-amd64.7z").Length > 0;
        var getKali = Button("Get Kali Image", () => {
            if (_kaliVerified || localKaliArchiveExists) OpenIsoFolder();
            else _ = DownloadLabImageAsync("kali");
        });
        getKali.IsEnabled = hypervisorReady && (!_downloadingKali || localKaliArchiveExists);
        if (_kaliVerified) { getKali.Content = "Kali downloaded"; getKali.BorderBrush = Paint(Green); }
        else if (localKaliArchiveExists) {
            getKali.Content = "Kali image found";
            ToolTip.SetTip(getKali, "Open ISO folder. SHA-256 verification may still be running.");
        }
        _kaliDownloadButton = getKali;
        attackerActions.Children.Add(getKali);
        attackerActions.Children.Add(Txt("→", 19, Accent, FontWeight.Bold));
        var extractedKali = Button("Extracted", () => {
            if (_installedKaliFolder is not null && Directory.Exists(_installedKaliFolder))
                Process.Start(new ProcessStartInfo(_installedKaliFolder) { UseShellExecute = true });
        });
        extractedKali.IsEnabled = _installedKaliFolder is not null && Directory.Exists(_installedKaliFolder);
        extractedKali.BorderBrush = Paint(extractedKali.IsEnabled ? Green : "#355368");
        _extractedKaliButton = extractedKali;
        attackerActions.Children.Add(extractedKali);
        attackerActions.Children.Add(Txt("→", 19, Accent, FontWeight.Bold));
        var installAttacker = Button(_installingAttacker ? "Installing..." : "Install Attacker",
            () => { if (_kaliConfigured) _ = OpenKaliVirtualBoxAsync(); else _ = InstallAttackerAsync(); });
        installAttacker.IsEnabled = hypervisorReady && _kaliVerified && !_installingAttacker;
        if (_kaliConfigured) {
            installAttacker.Content = "Open Kali";
            installAttacker.BorderBrush = Paint(Green);
        }
        _installAttackerButton = installAttacker;
        attackerActions.Children.Add(installAttacker);
        attackerControls.Children.Add(attackerActions);
        var attackerBody = Stack(3);
        var attackerHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var attackerRole = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        attackerRole.Children.Add(new IsometricLabIcon { Kind = "kali-workstation" });
        var attackerRoleLabel = Txt("ATTACKER", 15, Yellow, FontWeight.Bold);
        attackerRoleLabel.VerticalAlignment = VerticalAlignment.Center;
        attackerRole.Children.Add(attackerRoleLabel);
        Grid.SetColumn(attackerRole, 0);
        attackerHeader.Children.Add(attackerRole);
        _distro.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_distro, 1);
        // Fixed attacker distribution: no selection required.
        attackerBody.Children.Add(attackerHeader);
        attackerBody.Margin = new Thickness(10, 0, 0, 0);
        attackerBody.Children.Add(_attackerOs);
        _kaliRuntimeLabel = Txt(_kaliRunning ? "● ONLINE" : "● OFFLINE",
            13, _kaliRunning ? Green : Yellow, FontWeight.Bold);
        attackerBody.Children.Add(_kaliRuntimeLabel);
        attackerBody.Children.Add(VmDetails("IP ADDRESS   10.4.4.10 (not configured)\nOS VERSION   Kali Linux (not installed)\nUSER:PASS   kali:kali (default image)"));
        attackerControls.Margin = new Thickness(0, 10, 0, 0);
        attackerBody.Children.Add(attackerControls);
        var attackerLayout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,195"), ColumnSpacing = 6 };
        Grid.SetColumn(attackerBody, 0); attackerLayout.Children.Add(attackerBody);
        var attackerResources = VmResourceControls("attacker");
        attackerResources.Margin = new Thickness(0);
        Grid.SetColumn(attackerResources, 1); attackerLayout.Children.Add(attackerResources);
        var attackerCard = Stack(1);
        attackerCard.Children.Add(attackerLayout);
        var attackerProgressArea = Stack(2);
        attackerProgressArea.Margin = new Thickness(0);
        var attackerProgressHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        attackerProgressHeader.Children.Add(_attackerProgressText);
        var attackerHashActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        _kaliHashLabel = Txt(_kaliSha256 is null ? "" : "SHA-256: " + _kaliSha256[..12] + "…", 10, Muted);
        attackerHashActions.Children.Add(_kaliHashLabel);
        var attackerCopy = Button("Copy", () => {
            var hash = _kaliSha256;
            if (hash is not null && Clipboard is not null) _ = Clipboard.SetTextAsync(hash);
        });
        attackerCopy.Padding = new Thickness(6, 1);
        attackerCopy.FontSize = 10;
        attackerCopy.IsEnabled = _kaliSha256 is not null;
        _attackerCopyButton = attackerCopy;
        attackerHashActions.Children.Add(attackerCopy);
        Grid.SetColumn(attackerHashActions, 1);
        attackerProgressHeader.Children.Add(attackerHashActions);
        attackerProgressArea.Children.Add(attackerProgressHeader);

        attackerProgressArea.Children.Add(_attackerProgress);
        attackerCard.Children.Add(attackerProgressArea);
        var attacker = Tile(attackerCard); attacker.Padding = new Thickness(7);
        attacker.BorderBrush = Paint(_kaliRunning ? Green : Yellow);
        attacker.BorderThickness = new Thickness(2);
        _kaliOuterTile = attacker;
        var attackerLayer = WithLayer(attacker, "LAYER 03", "Virtual Machines  /  Attacker");
        Grid.SetColumn(attackerLayer, 0); network.Children.Add(attackerLayer);
        var link = Stack(3);
        link.VerticalAlignment = VerticalAlignment.Center;
        link.HorizontalAlignment = HorizontalAlignment.Stretch;
        var networkName = Txt("aisecurelab", 13, Text, FontWeight.SemiBold);
        networkName.TextAlignment = TextAlignment.Center;
        var networkType = Txt("INTERNAL NETWORK", 10, Muted);
        networkType.TextAlignment = TextAlignment.Center;
        var networkArrow = Txt("<------>", 20, Accent, FontWeight.Bold);
        networkArrow.TextAlignment = TextAlignment.Center;
        var networkIcon = new IsometricLabIcon { Kind = "network", HorizontalAlignment = HorizontalAlignment.Center };
        link.Children.Add(networkIcon);
        link.Children.Add(networkArrow);
        link.Children.Add(networkName);
        link.Children.Add(networkType);
        var networkState = Txt("● OFFLINE", 11, Yellow, FontWeight.Bold);
        networkState.TextAlignment = TextAlignment.Center;
        link.Children.Add(networkState);
        var configureNetwork = Button("Configure", () =>
            _activity.Text = "Network configuration is not implemented yet. No changes made.");
        configureNetwork.IsEnabled = hypervisorReady;
        configureNetwork.HorizontalAlignment = HorizontalAlignment.Center;
        link.Children.Add(configureNetwork);
        var linkTile = new Border {
            Background = Paint("#24394D"), CornerRadius = new CornerRadius(10),
            BorderBrush = Paint("#355368"), BorderThickness = new Thickness(1),
            Padding = new Thickness(9, 9), VerticalAlignment = VerticalAlignment.Center,
            Child = link
        };
        // Network stays a normal interactive card; the overlay owns every circuit line.
        var networkColumn = new Grid { RowDefinitions = new RowDefinitions("*,Auto,*") };
        Grid.SetRow(linkTile, 1);
        networkColumn.Children.Add(linkTile);
        Grid.SetColumn(networkColumn, 1);
        network.Children.Add(networkColumn);
        var targetActions = Stack(2);
        targetActions.Margin = new Thickness(0, 10, 0, 0);
        targetActions.Children.Add(TargetControls(hypervisorReady));
        var target = Machine("▣", "VICTIM / TARGET", "Debian 13",
            "● OFFLINE", Yellow,
            "IP ADDRESS   10.4.4.20 (not configured)\nOS VERSION   Debian 13 (not installed)\nUSER:PASS   Set during installation",
            targetActions);
        target.Padding = new Thickness(10, 0, 0, 0);
        var targetLayout = new Grid { ColumnDefinitions = new ColumnDefinitions("*,195"), ColumnSpacing = 6 };
        Grid.SetColumn(target, 0); targetLayout.Children.Add(target);
        var targetResources = VmResourceControls("target");
        targetResources.Margin = new Thickness(0);
        Grid.SetColumn(targetResources, 1); targetLayout.Children.Add(targetResources);
        var targetCard = Stack(1);
        targetCard.Children.Add(targetLayout);
        var targetProgressArea = Stack(2);
        targetProgressArea.Margin = new Thickness(0);
        var targetProgressHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        targetProgressHeader.Children.Add(_targetProgressText);
        var targetHashActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        _debianHashLabel = Txt(_debianSha256 is null ? "" : "SHA-256: " + _debianSha256[..12] + "…", 10, Muted);
        targetHashActions.Children.Add(_debianHashLabel);
        var targetCopy = Button("Copy", () => {
            var hash = _debianSha256;
            if (hash is not null && Clipboard is not null) _ = Clipboard.SetTextAsync(hash);
        });
        targetCopy.Padding = new Thickness(6, 1);
        targetCopy.FontSize = 10;
        targetCopy.IsEnabled = _debianSha256 is not null;
        _targetCopyButton = targetCopy;
        targetHashActions.Children.Add(targetCopy);
        Grid.SetColumn(targetHashActions, 1);
        targetProgressHeader.Children.Add(targetHashActions);
        targetProgressArea.Children.Add(targetProgressHeader);

        targetProgressArea.Children.Add(_targetProgress);
        targetCard.Children.Add(targetProgressArea);
        var targetTile = Tile(targetCard); targetTile.Padding = new Thickness(7);
        SetReadyOutline(targetTile, false); // ISO attachment is not VM validation.
        var targetLayer = WithLayer(targetTile, "LAYER 03", "Virtual Machines  /  Victim");
        Grid.SetColumn(targetLayer, 2); network.Children.Add(targetLayer);
        layers.Children.Add(network);
        // VM connection remains muted until guests are actually verified.


        // The hypervisor is the foundation beneath both guest machines.

        var foundationInfo = Stack(2);
        foundationInfo.HorizontalAlignment = HorizontalAlignment.Stretch;
        foundationInfo.Children.Add(new IsometricLabIcon { Kind = "hypervisor", HorizontalAlignment = HorizontalAlignment.Center });
        foreach (var line in new[] {
            Txt("HYPERVISOR  /  ORACLE VIRTUALBOX", 16, Accent, FontWeight.Bold),
            Txt("Virtualization layer hosting Attacker + Victim VMs", 13, Muted),
            Txt(hypervisorReady ? "SUPPORTED  -  INSTALLED" : "NOT INSTALLED  -  REQUIRED FIRST",
                13, hypervisorReady ? Green : Red, FontWeight.Bold)
        })
        {
            line.TextAlignment = TextAlignment.Center;
            line.HorizontalAlignment = HorizontalAlignment.Stretch;
            foundationInfo.Children.Add(line);
        }
        Button? installHypervisorPulse = null;
        if (hypervisorReady)
        {
            var uninstallHypervisor = Button("Uninstall VirtualBox", () => _ = UninstallVirtualBoxAsync());
            uninstallHypervisor.BorderBrush = Paint(Red);
            uninstallHypervisor.HorizontalAlignment = HorizontalAlignment.Center;
            foundationInfo.Children.Add(uninstallHypervisor);
        }
        else
        {
            var installHypervisor = Button("Install VirtualBox", () => _ = InstallVirtualBoxAsync());
            installHypervisorPulse = installHypervisor;
            installHypervisor.HorizontalAlignment = HorizontalAlignment.Center;
            foundationInfo.Children.Add(installHypervisor);
        }
        var foundationCard = Tile(foundationInfo, "#20384A");
        SetReadyOutline(foundationCard, hypervisorReady);
        var foundationTile = WithLayer(foundationCard, "LAYER 02", "Hypervisor  /  Virtualization platform");
        layers.Children.Add(foundationTile);
        // Physical host is the bottom layer of the topology, beneath VirtualBox.
        // Host hardware and OS are the physical foundation of the VM stack.
        var build = Environment.OSVersion.Version.Build;
        var windows = OperatingSystem.IsWindows();
        var architecture = RuntimeInformation.OSArchitecture;
        var archOk = windows && architecture == Architecture.X64 && build >= 22000;
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var free = drive.AvailableFreeSpace / 1073741824d;
        var diskOk = free >= 35;
        var physicalHost = Stack(2);
        var hostTitle = Txt("HOST MACHINE", 15, Accent, FontWeight.Bold);
        hostTitle.TextAlignment = TextAlignment.Center;
        hostTitle.HorizontalAlignment = HorizontalAlignment.Stretch;
        physicalHost.Children.Add(hostTitle);
        var hostSpecs = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"), ColumnSpacing = 10 };
        var architectureTile = Requirement("ARCHITECTURE", architecture.ToString(), State(archOk), archOk ? Green : Red,
            "Windows 11 x64 is required for this Windows preview.", null);
        var diskTile = Requirement("DISK SPACE", $"{free:F1} GB free", diskOk ? "SUPPORTED" : "NOT ENOUGH SPACE", diskOk ? Green : Red,
            "Minimum 35 GB free for the initial target VM. More space is recommended for two VMs.", null);
        var osName = windows && build >= 22000 ? "Windows 11" : RuntimeInformation.OSDescription;
        var osTile = Requirement("OPERATING SYSTEM", osName,
            windows && build >= 22000 ? "SUPPORTED" : "NOT SUPPORTED",
            windows && build >= 22000 ? Green : Red,
            "Windows 11 is required for this Windows desktop preview.", null);
        Grid.SetColumn(osTile, 0); hostSpecs.Children.Add(osTile);
        Grid.SetColumn(architectureTile, 1); hostSpecs.Children.Add(architectureTile);
        Grid.SetColumn(diskTile, 2); hostSpecs.Children.Add(diskTile);
        var physicalRam = GetPhysicalRamGiB();
        var ramOk = physicalRam is >= 8;
        var ramTile = Requirement("PHYSICAL RAM",
            physicalRam is null ? "Unavailable" : $"{physicalRam.Value:F1} GB total",
            physicalRam is null ? "CHECK UNAVAILABLE" : ramOk ? "SUPPORTED" : "8 GB RECOMMENDED",
            physicalRam is null ? Yellow : ramOk ? Green : Yellow,
            "8 GB or more is recommended for running Kali and Debian together.", null);
        Grid.SetColumn(ramTile, 3); hostSpecs.Children.Add(ramTile);
        var logicalProcessors = Environment.ProcessorCount;
        var cpuTile = Requirement("LOGICAL PROCESSORS", logicalProcessors.ToString(),
            logicalProcessors >= 4 ? "SUPPORTED" : "4 RECOMMENDED",
            logicalProcessors >= 4 ? Green : Yellow,
            "Four logical processors or more are recommended for running both VMs.", null);
        Grid.SetColumn(cpuTile, 4); hostSpecs.Children.Add(cpuTile);
        physicalHost.Children.Add(hostSpecs);
        // Only confirmed host prerequisites and installed hypervisor turn this link green.

        var hostReady = archOk && diskOk && ramOk && logicalProcessors >= 4;
        var hostCard = Tile(physicalHost, "#1B3042");
        SetReadyOutline(hostCard, hostReady);
        var hostTile = WithLayer(hostCard, "LAYER 01", "Physical Host  /  Hardware and operating system");
        layers.Children.Add(hostTile);
        // One overlay draws all connections using live control positions.
        var circuitOverlay = new TopologyConnections {
            Attacker = attackerLayer,
            Network = linkTile,
            Victim = targetLayer,
            Hypervisor = foundationTile,
            Host = hostTile,
            HypervisorReady = hypervisorReady && archOk && diskOk && ramOk && logicalProcessors >= 4,
            IsHitTestVisible = false
        };
        var circuitLayout = new Grid();
        circuitLayout.Children.Add(layers);
        circuitLayout.Children.Add(circuitOverlay);
        circuitLayout.LayoutUpdated += (_, _) => circuitOverlay.InvalidateVisual();
        var circuitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        circuitTimer.Tick += (_, _) => {
            if (++_runtimePollTicks % 7 == 0) _ = RefreshKaliRuntimeAsync();
            circuitOverlay.Bright = !circuitOverlay.Bright;
            circuitOverlay.InvalidateVisual();
            // Configured Kali stays solid green; only pending connections pulse.
            if (_kaliConfigured)
            {
                attacker.BorderBrush = Paint(_kaliRunning ? Green : Yellow);
                installAttacker.BorderBrush = Paint(Green);
            }
            if (installHypervisorPulse is not null)
                installHypervisorPulse.BorderBrush = Paint(circuitOverlay.Bright ? "#85EAF1" : "#397389");
        };
        _circuitTimer = circuitTimer;
        circuitTimer.Start();
        topology.Children.Add(circuitLayout);
        var architecturePanel = new Border {
            Background = Paint("#0F2030"), CornerRadius = new CornerRadius(16),
            BorderBrush = Paint("#294153"), BorderThickness = new Thickness(1),
            Padding = new Thickness(11), Child = topology
        };
        _page.Children.Add(architecturePanel);

        var isoLocation = Stack(7);
        var isoHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        isoHeader.Children.Add(new IsometricLabIcon { Kind = "folder" });
        var isoLabel = Txt("ISO STORAGE  /  HYDRATION KIT", 13, Accent, FontWeight.Bold);
        isoLabel.VerticalAlignment = VerticalAlignment.Center;
        isoHeader.Children.Add(isoLabel);
        isoLocation.Children.Add(isoHeader);
        var storageRoot = Path.GetDirectoryName(Path.GetDirectoryName(_isoRoot)!)!;
        var extractionRoot = Path.Combine(_root, "virtual-machines", "kali");
        var storageTree = Stack(3);
        storageTree.Children.Add(Txt("STORAGE LOCATIONS", 11, Accent, FontWeight.Bold));
        void AddStoragePath(string prefix, string label, string path)
        {
            var entry = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var name = Txt(prefix + " " + label, 12, Muted);
            name.VerticalAlignment = VerticalAlignment.Center;
            entry.Children.Add(name);
            var link = Txt(path, 12, Accent);
            link.VerticalAlignment = VerticalAlignment.Center;
            link.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            link.TextDecorations = TextDecorations.Underline;
            ToolTip.SetTip(link, "Open folder: " + path);
            link.PointerEntered += (_, _) => link.Foreground = Paint("#B3F6FF");
            link.PointerExited += (_, _) => link.Foreground = Paint(Accent);
            link.PointerPressed += (_, _) => OpenStoragePath(path);
            entry.Children.Add(link);
            storageTree.Children.Add(entry);
        }
        AddStoragePath("▣", "HYDRATION KIT ROOT", storageRoot);
        AddStoragePath("├─", "DOWNLOADS / ISOS", _isoRoot);
        AddStoragePath("├─", "EXTRACTED VMS", extractionRoot);
        var temporaryRoot = Path.Combine(_root, "virtual-machines");
        AddStoragePath("└─", "VM WORKSPACE / TEMPORARY", temporaryRoot);
        var extractedSessions = Directory.Exists(extractionRoot)
            ? Directory.GetDirectories(extractionRoot).OrderByDescending(p => p).Take(5).ToArray()
            : Array.Empty<string>();
        foreach (var session in extractedSessions)
            AddStoragePath("   └─", Path.GetFileName(session), session);
        isoLocation.Children.Add(storageTree);
        var openStorageRoot = Button("Open Hydration Kit Root", () => OpenStoragePath(storageRoot));
        isoLocation.Children.Add(openStorageRoot);
        _page.Children.Add(Tile(isoLocation, "#122432"));

        _page.Children.Add(Txt("IPs are planned static addresses (10.4.4.0/24). ACTIVE will require both guests to be verified, their interfaces configured and bidirectional connectivity tested.", 12, Muted));

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), Margin = new Thickness(0, 20, 0, 12) };
        var nextInfo = Txt("STEP 2 OF 2  /  LEARNING PATH  -  Available after lab validation", 12, Muted);
        nextInfo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(nextInfo, 0);
        footer.Children.Add(nextInfo);

        var exitButton = Button("Exit", Close);
        exitButton.MinWidth = 220;
        exitButton.Padding = new Thickness(28, 13);
        exitButton.Background = Paint("#142B42");
        exitButton.BorderBrush = Paint("#FF6674");
        exitButton.BorderThickness = new Thickness(1.5);
        exitButton.Foreground = Paint("#F0F5FA");
        exitButton.CornerRadius = new CornerRadius(9);
        exitButton.FontWeight = FontWeight.Bold;
        exitButton.HorizontalAlignment = HorizontalAlignment.Center;
        exitButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        exitButton.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(exitButton, 1);
        footer.Children.Add(exitButton);

        _next.Content = "Next  >";
        _next.IsEnabled = false;
        _next.MinWidth = 155;
        _next.HorizontalAlignment = HorizontalAlignment.Right;
        _next.Background = Paint("#142B42");
        _next.BorderBrush = Paint("#53DCE8");
        _next.BorderThickness = new Thickness(2);
        _next.HorizontalContentAlignment = HorizontalAlignment.Center;
        _next.VerticalContentAlignment = VerticalAlignment.Center;
        _next.Foreground = Paint("#F0F5FA");
        _next.CornerRadius = new CornerRadius(9);
        _next.FontWeight = FontWeight.SemiBold;
        _next.Classes.Add("academy-next");
        _next.PropertyChanged += (_, args) =>
        {
            if (args.Property == Avalonia.Controls.Button.IsEnabledProperty)
            {
                _next.Background = Paint(_next.IsEnabled ? "#176B80" : "#142B42");
                _next.BorderBrush = Paint(_next.IsEnabled ? "#85EAF1" : "#53DCE8");
                _next.Foreground = Paint(_next.IsEnabled ? "#FFFFFF" : "#A3B5C7");
            }
        };
        Grid.SetColumn(_next, 2);
        footer.Children.Add(_next);
        _page.Children.Add(footer);
    }

    private void MarkKaliExtracted(string folder)
    {
        if (!Directory.Exists(folder)) return;
        _installedKaliFolder = folder;
        if (_extractedKaliButton is not null)
        {
            _extractedKaliButton.IsEnabled = true;
            _extractedKaliButton.BorderBrush = Paint(Green);
            ToolTip.SetTip(_extractedKaliButton, folder);
        }
    }

    private async Task RefreshKaliRuntimeAsync()
    {
        if (_checkingKaliRuntime) return;
        _checkingKaliRuntime = true;
        try
        {
            var vbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Oracle", "VirtualBox", "VBoxManage.exe");
            if (!File.Exists(vbox)) return;
            var start = new ProcessStartInfo(vbox) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add("list");
            start.ArgumentList.Add("runningvms");
            using var process = Process.Start(start);
            if (process is null) return;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await stdout;
            await stderr;
            if (process.ExitCode != 0) return;
            var running = System.Text.RegularExpressions.Regex.IsMatch(output,
                @"(?im)^""[^""]*kali-linux[^""]*""\s+\{[a-f0-9-]+\}");
            _kaliRunning = running;
            if (_kaliRuntimeLabel is not null)
            {
                _kaliRuntimeLabel.Text = running ? "● ONLINE" : "● OFFLINE";
                _kaliRuntimeLabel.Foreground = Paint(running ? Green : Yellow);
                if (_kaliOuterTile is not null) _kaliOuterTile.BorderBrush = Paint(running ? Green : Yellow);
            }
        }
        catch { /* Leave last known status if VirtualBox cannot be queried. */ }
        finally { _checkingKaliRuntime = false; }
    }

    private async Task DetectRegisteredKaliAsync()
    {
        if (_installingAttacker) return;
        var vbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Oracle", "VirtualBox", "VBoxManage.exe");
        if (!File.Exists(vbox)) return;
        try
        {
            var start = new ProcessStartInfo(vbox) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add("list");
            start.ArgumentList.Add("vms");
            using var p = Process.Start(start);
            if (p is null) return;
            var outputTask = p.StandardOutput.ReadToEndAsync();
            var errorTask = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            var output = await outputTask;
            await errorTask;
            if (p.ExitCode != 0) return;
            var matches = System.Text.RegularExpressions.Regex.Matches(output,
                @"(?im)^""([^""]*kali-linux[^""]*)""\s+\{([a-f0-9-]+)\}");
            if (matches.Count != 1) return;
            var uuid = matches[0].Groups[2].Value;
            var infoStart = new ProcessStartInfo(vbox) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] { "showvminfo", uuid, "--machinereadable" })
                infoStart.ArgumentList.Add(arg);
            using var info = Process.Start(infoStart);
            if (info is null) return;
            var infoTextTask = info.StandardOutput.ReadToEndAsync();
            var infoErrorTask = info.StandardError.ReadToEndAsync();
            await info.WaitForExitAsync();
            var details = await infoTextTask;
            await infoErrorTask;
            if (info.ExitCode != 0) return;
            // Registration alone is not configuration: require internal networking.
            if (!details.Contains("nic1=\"intnet\"", StringComparison.OrdinalIgnoreCase) ||
                !details.Contains("intnet1=\"aisecurelab\"", StringComparison.OrdinalIgnoreCase))
                return;
            if (_kaliConfigured) return;
            _kaliConfigured = true;
            if (!_kaliVerified) _kaliStatus = "Kali registered and configured - guest verification pending";
            _kaliPercent = 100;
            _attackerProgressText.Text = _kaliStatus;
            _attackerProgress.Value = 100;
            if (_installAttackerButton is not null)
            {
                _installAttackerButton.Content = "Open Kali";
                _installAttackerButton.BorderBrush = Paint(Green);
                _installAttackerButton.IsEnabled = true;
            }
            // Rebuild the card only once when the registration is discovered.
            if (!_installingAttacker) ShowSetup();
        }
        catch { /* Diagnostics are best-effort; keep manual install available. */ }
    }

    private async Task OpenKaliVirtualBoxAsync()
    {
        var vbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Oracle", "VirtualBox", "VBoxManage.exe");
        if (!File.Exists(vbox)) { _attackerProgressText.Text = "VirtualBox not found."; return; }
        try
        {
            var lookup = new ProcessStartInfo(vbox) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            lookup.ArgumentList.Add("list");
            lookup.ArgumentList.Add("vms");
            using var p = Process.Start(lookup) ?? throw new InvalidOperationException("Cannot query VirtualBox.");
            var outputTask = p.StandardOutput.ReadToEndAsync();
            var errorTask = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;
            if (p.ExitCode != 0) throw new InvalidOperationException(error);
            var matches = System.Text.RegularExpressions.Regex.Matches(output,
                @"(?m)^""([^""]*kali-linux[^""]*)""\s+\{([a-fA-F0-9-]+)\}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (matches.Count != 1)
                throw new InvalidOperationException("Expected exactly one registered Kali VM; found " + matches.Count);
            var uuid = matches[0].Groups[2].Value;
            var start = new ProcessStartInfo(vbox) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] { "showvminfo", uuid, "--machinereadable" }) start.ArgumentList.Add(arg);
            using var info = Process.Start(start) ?? throw new InvalidOperationException("Cannot inspect Kali VM.");
            var stateTask = info.StandardOutput.ReadToEndAsync();
            var errTask = info.StandardError.ReadToEndAsync();
            await info.WaitForExitAsync();
            var state = await stateTask;
            var stateError = await errTask;
            if (info.ExitCode != 0) throw new InvalidOperationException(stateError);
            if (state.Contains("VMState=\"running\"", StringComparison.OrdinalIgnoreCase))
            {
                _attackerProgressText.Text = "Kali is already running in VirtualBox.";
                return;
            }
            if (!state.Contains("VMState=\"poweroff\"", StringComparison.OrdinalIgnoreCase) &&
                !state.Contains("VMState=\"aborted\"", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Kali is not in a startable state.");
            var launch = new ProcessStartInfo(vbox) { UseShellExecute = true };
            foreach (var arg in new[] { "startvm", uuid, "--type", "gui" }) launch.ArgumentList.Add(arg);
            Process.Start(launch);
            _attackerProgressText.Text = "Opening Kali VM in VirtualBox (guest boot not yet verified).";
        }
        catch (Exception ex) { _attackerProgressText.Text = "Cannot open Kali: " + ex.Message; }
    }

    private async Task InstallAttackerAsync()
    {
        if (_installingAttacker) return;
        if (!_kaliVerified || _verifiedKaliPath is null || !File.Exists(_verifiedKaliPath))
        {
            _attackerProgressText.Text = "Kali archive not verified in this session. Recheck the image before installation.";
            _ = DownloadLabImageAsync("kali");
            return;
        }
        var vbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Oracle", "VirtualBox", "VBoxManage.exe");
        if (!File.Exists(vbox))
        {
            _kaliStatus = "VirtualBox is not installed";
            _attackerProgressText.Text = _kaliStatus;
            return;
        }
        var confirmation = new Window {
            Title = "Register Kali VM", Width = 520, Height = 220,
            Background = Paint("#152332"), WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var confirmationBody = Stack(12);
        confirmationBody.Margin = new Thickness(20);
        confirmationBody.Children.Add(Txt("Extract the verified Kali image and register it in VirtualBox?", 16, Text));
        confirmationBody.Children.Add(Txt("This will create VM files and register a machine. It will not start the guest or modify other VMs.", 12, Muted));
        var confirmationButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        confirmationButtons.Children.Add(Button("Cancel", () => confirmation.Close(false)));
        confirmationButtons.Children.Add(Button("Register Kali", () => confirmation.Close(true)));
        confirmationBody.Children.Add(confirmationButtons);
        confirmation.Content = confirmationBody;
        if (!await confirmation.ShowDialog<bool>(this)) return;

        _installingAttacker = true;
        if (_installAttackerButton is not null) {
            _installAttackerButton.IsEnabled = false;
            _installAttackerButton.Content = "Installing...";
        }
        // Archive extraction has no reliable percentage from tar.exe.
        // Show an active indeterminate bar rather than inventing progress.
        _attackerProgress.IsIndeterminate = true;
        _attackerProgressText.Text = "Preparing Kali extraction...";
        try
        {
            // Each installation attempt gets its own timestamped workspace.
            // Only abandoned attempts created by this installer may be cleaned.
            var kaliRoot = Path.Combine(_root, "virtual-machines", "kali");
            Directory.CreateDirectory(kaliRoot);
            var list = new ProcessStartInfo(vbox) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            list.ArgumentList.Add("list");
            list.ArgumentList.Add("vms");
            list.ArgumentList.Add("--long");
            using var registered = Process.Start(list)
                ?? throw new InvalidOperationException("Cannot inspect registered VirtualBox VMs.");
            var vmOutputTask = registered.StandardOutput.ReadToEndAsync();
            var vmErrorTask = registered.StandardError.ReadToEndAsync();
            await registered.WaitForExitAsync();
            var vmOutput = await vmOutputTask;
            var vmError = await vmErrorTask;
            if (registered.ExitCode != 0)
                throw new InvalidOperationException("Cannot verify existing VMs: " + vmError);
            // A registered Kali VM is already installed: configure it directly.
            // Do not extract the multi-GB archive again on every click.
            var registeredKali = System.Text.RegularExpressions.Regex.Matches(vmOutput,
                    @"(?m)^Name:\s*(kali-linux-[^\r\n]+)\r?$")
                .Cast<System.Text.RegularExpressions.Match>().ToList();
            if (registeredKali.Count == 1)
            {
                var existingName = registeredKali[0].Groups[1].Value.Trim();
                var infoStart = new ProcessStartInfo(vbox) {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                foreach (var arg in new[] { "showvminfo", existingName, "--machinereadable" })
                    infoStart.ArgumentList.Add(arg);
                using var infoProcess = Process.Start(infoStart)
                    ?? throw new InvalidOperationException("Cannot inspect existing Kali VM.");
                var infoOutputTask = infoProcess.StandardOutput.ReadToEndAsync();
                var infoErrorTask = infoProcess.StandardError.ReadToEndAsync();
                await infoProcess.WaitForExitAsync();
                var infoOutput = await infoOutputTask;
                var infoError = await infoErrorTask;
                if (infoProcess.ExitCode != 0)
                    throw new InvalidOperationException("Cannot inspect Kali VM: " + infoError);
                if (!infoOutput.Contains("VMState=\"poweroff\"", StringComparison.OrdinalIgnoreCase) &&
                    !infoOutput.Contains("VMState=\"aborted\"", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Existing Kali VM must be powered off before configuration.");
                _attackerProgress.IsIndeterminate = false;
                _attackerProgress.Value = 90;
                _attackerProgressText.Text = "Existing Kali VM found - applying settings (no extraction)...";
                var modify = new ProcessStartInfo(vbox) {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                foreach (var arg in new[] { "modifyvm", existingName,
                    "--memory", (Math.Clamp(ReadVmResource("attacker", "memory", 2), 1, 8) * 1024).ToString(),
                    "--cpus", Math.Clamp(ReadVmResource("attacker", "processors", 2), 1, 4).ToString(),
                    "--nic1", "intnet", "--intnet1", "aisecurelab" })
                    modify.ArgumentList.Add(arg);
                using var modifying = Process.Start(modify)
                    ?? throw new InvalidOperationException("Cannot configure Kali VM.");
                var modifyOutputTask = modifying.StandardOutput.ReadToEndAsync();
                var modifyErrorTask = modifying.StandardError.ReadToEndAsync();
                await modifying.WaitForExitAsync();
                await modifyOutputTask;
                var modifyError = await modifyErrorTask;
                if (modifying.ExitCode != 0)
                    throw new InvalidOperationException("Kali configuration failed: " + modifyError);
                var cfgMatch = System.Text.RegularExpressions.Regex.Match(infoOutput,
                    @"(?m)^CfgFile=""([^""]+)"""); 
                if (cfgMatch.Success)
                    MarkKaliExtracted(Path.GetDirectoryName(cfgMatch.Groups[1].Value)!);
                _attackerProgress.Value = 100;
                _attackerProgressText.Text = "Kali registered and configured - guest verification pending";
                _kaliConfigured = true;
                return;
            }
            if (registeredKali.Count > 1)
                throw new InvalidOperationException("Multiple registered Kali VMs found. Choose one in VirtualBox before proceeding.");
            var todayPrefix = DateTime.Now.ToString("yyyyMMdd") + "-";
            foreach (var previous in Directory.GetDirectories(kaliRoot, todayPrefix + "*"))
            {
                var incompleteMarker = Path.Combine(previous, ".aisecure-extraction-incomplete");
                if (!File.Exists(incompleteMarker)) continue;
                if (vmOutput.Contains(previous, StringComparison.OrdinalIgnoreCase)) continue;
                try { Directory.Delete(previous, recursive: true); }
                catch (IOException) { /* Keep locked files and use a fresh workspace. */ }
                catch (UnauthorizedAccessException) { /* Never force-delete locked files. */ }
            }
            var workspace = Path.Combine(kaliRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(workspace);
            var extractionMarker = Path.Combine(workspace, ".aisecure-extraction-incomplete");
            File.WriteAllText(extractionMarker, "Owned by AI Secure Hydration Kit; incomplete extraction.");
            _attackerProgressText.Text = "Extracting Kali archive...";
            // Read archive member names to estimate extraction progress by
            // completed files. This is an estimate, not a byte-accurate percent.
            var listArchive = new ProcessStartInfo("tar.exe") {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            listArchive.ArgumentList.Add("-tf");
            listArchive.ArgumentList.Add(_verifiedKaliPath);
            using var archiveListing = Process.Start(listArchive)
                ?? throw new InvalidOperationException("Cannot list Kali archive.");
            var listingTask = archiveListing.StandardOutput.ReadToEndAsync();
            var listingErrorTask = archiveListing.StandardError.ReadToEndAsync();
            await archiveListing.WaitForExitAsync();
            var listingOutput = await listingTask;
            var listingError = await listingErrorTask;
            if (archiveListing.ExitCode != 0)
                throw new InvalidOperationException("Cannot inspect Kali archive: " + listingError);
            var members = listingOutput.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.EndsWith("/") && !p.EndsWith("\\\\"))
                .ToArray();
            var extraction = new ProcessStartInfo("tar.exe") {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true
            };
            extraction.ArgumentList.Add("-xf");
            extraction.ArgumentList.Add(_verifiedKaliPath);
            extraction.ArgumentList.Add("-C");
            extraction.ArgumentList.Add(workspace);
            _attackerProgress.IsIndeterminate = members.Length == 0;
            using (var process = Process.Start(extraction)
                ?? throw new InvalidOperationException("Cannot start tar.exe"))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                while (!process.HasExited)
                {
                    if (members.Length > 0)
                    {
                        var completed = members.Count(member =>
                        {
                            var relative = member.Replace('/', Path.DirectorySeparatorChar);
                            var full = Path.GetFullPath(Path.Combine(workspace, relative));
                            return full.StartsWith(Path.GetFullPath(workspace) + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase) && File.Exists(full);
                        });
                        var percent = Math.Min(69, completed * 70d / members.Length);
                        _attackerProgress.Value = percent;
                        _attackerProgressText.Text = $"Extracting Kali (approx. {percent:F0}%)...";
                    }
                    await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(500));
                }
                await process.WaitForExitAsync();
                var error = await stderr;
                await stdout;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Kali extraction failed: " + error);
            }
            File.Delete(extractionMarker);
            _attackerProgress.IsIndeterminate = false;
            _attackerProgress.Value = 70;
            _attackerProgressText.Text = "Extraction complete - locating VM configuration...";
            var configs = Directory.GetFiles(workspace, "*.vbox", SearchOption.AllDirectories);
            if (configs.Length != 1)
                throw new InvalidOperationException($"Expected one Kali .vbox file; found {configs.Length}. No VM registered.");
            _attackerProgress.Value = 80;
            _attackerProgressText.Text = "Registering Kali in VirtualBox...";
            async Task<string> VBox(params string[] args)
            {
                var start = new ProcessStartInfo(vbox) {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true, RedirectStandardOutput = true
                };
                foreach (var arg in args) start.ArgumentList.Add(arg);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start VBoxManage");
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                var output = await stdout;
                var error = await stderr;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("VBoxManage failed: " + error);
                return output;
            }
            var config = configs[0];
            var vmDocument = System.Xml.Linq.XDocument.Load(config);
            var machineElement = vmDocument.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Machine")
                ?? throw new InvalidOperationException("Kali .vbox has no Machine element.");
            var vmUuid = machineElement.Attribute("uuid")?.Value?.Trim('{', '}')
                ?? throw new InvalidOperationException("Kali .vbox has no UUID.");
            if (!Guid.TryParse(vmUuid, out var parsedUuid))
                throw new InvalidOperationException("Kali .vbox contains an invalid UUID.");
            var vmId = parsedUuid.ToString("D");
            var existing = await VBox("list", "vms");
            var alreadyRegistered = existing.Contains("{" + vmId + "}", StringComparison.OrdinalIgnoreCase);
            if (alreadyRegistered)
            {
                _attackerProgressText.Text = "Kali VM already registered - reusing existing machine...";
                // The fresh extraction is redundant. Delete only this newly
                // created timestamped directory, never the registered VM files.
                Directory.Delete(workspace, recursive: true);
            }
            else
            {
                await VBox("registervm", config);
            }
            _attackerProgress.Value = 90;
            _attackerProgressText.Text = "Applying Kali memory, CPUs and internal network...";
            // Address the machine by UUID: names may differ or be duplicated.
            var state = await VBox("showvminfo", vmId, "--machinereadable");
            if (!state.Contains("VMState=\"poweroff\"", StringComparison.OrdinalIgnoreCase) &&
                !state.Contains("VMState=\"aborted\"", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Kali VM is not powered off. Shut it down before changing its resources.");
            await VBox("modifyvm", vmId, "--memory",
                (Math.Clamp(ReadVmResource("attacker", "memory", 2), 1, 8) * 1024).ToString(),
                "--cpus", Math.Clamp(ReadVmResource("attacker", "processors", 2), 1, 4).ToString(),
                "--nic1", "intnet", "--intnet1", "aisecurelab");
            var registeredInfo = await VBox("showvminfo", vmId, "--machinereadable");
            var cfgMatchFinal = System.Text.RegularExpressions.Regex.Match(registeredInfo,
                @"(?m)^CfgFile=""([^""]+)""");
            if (cfgMatchFinal.Success)
                MarkKaliExtracted(Path.GetDirectoryName(cfgMatchFinal.Groups[1].Value)!);
            _attackerProgressText.Text = "Kali registered and configured - guest verification pending";
            _kaliConfigured = true;
            _attackerProgress.Value = 100;
        }
        catch (Exception ex)
        {
            _attackerProgress.IsIndeterminate = false;
            _attackerProgressText.Text = "Kali setup failed: " + ex.Message;
        }
        finally
        {
            _attackerProgress.IsIndeterminate = false;
            _installingAttacker = false;
            if (_installAttackerButton is not null) {
                _installAttackerButton.IsEnabled = true;
                _installAttackerButton.Content = _kaliConfigured ? "Open Kali" : "Install Attacker";
                if (_kaliConfigured) _installAttackerButton.BorderBrush = Paint(Green);
            }
        }
    }

    private async Task DownloadLabImageAsync(string kind)
    {
        var kali = kind == "kali";
        if (kali ? _downloadingKali : _downloadingDebian) return;
        if (kali) _downloadingKali = true; else _downloadingDebian = true;
        void Status(string message, double percent)
        {
            if (kali) { _kaliStatus = message; _kaliPercent = percent; }
            else { _debianStatus = message; _debianPercent = percent; }
            SetInstallationProgress(kali ? "Attacker" : "Target", message, percent);
        }
        var actionButton = kali ? _kaliDownloadButton : _debianDownloadButton;
        if (actionButton is not null)
            actionButton.IsEnabled = kali && Directory.Exists(_isoRoot) &&
                Directory.GetFiles(_isoRoot, "kali-linux-*-virtualbox-amd64.7z").Length > 0;
        Status("Checking latest official image...", 0);
        try
        {
            using var http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AISecureAcademy-HydrationKit/1.0");
            Uri source;
            // Resolve the image filename from the official release directory,
            // rather than scraping a marketing page or pinning an old release.
            var directory = new Uri(kali
                ? "https://cdimage.kali.org/current/"
                : "https://cdimage.debian.org/debian-cd/current/amd64/iso-cd/");
            var listing = await http.GetStringAsync(directory);
            var pattern = kali
                ? @"kali-linux-[0-9.]+-virtualbox-amd64\.7z"
                : @"debian-[0-9.]+-amd64-netinst\.iso";
            var filenames = System.Text.RegularExpressions.Regex.Matches(listing, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (filenames.Count == 0)
                throw new InvalidOperationException("Official release image not found in " + directory);
            var selected = filenames.OrderByDescending(n => n, StringComparer.OrdinalIgnoreCase).First();
            source = new Uri(directory, selected);
            var filename = Uri.UnescapeDataString(source.Segments.Last());
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException("Invalid download filename.");
            Directory.CreateDirectory(_isoRoot);
            var destination = Path.Combine(_isoRoot, filename);
            var temporary = destination + ".part";
            // Fetch the publisher checksum manifest and verify a matching local file.
            var manifest = await http.GetStringAsync(new Uri(directory, "SHA256SUMS"));
            var expected = System.Text.RegularExpressions.Regex.Matches(manifest,
                    @"(?im)^([a-f0-9]{64})\s+\*?(.+?)\s*$")
                .Cast<System.Text.RegularExpressions.Match>()
                .FirstOrDefault(m => Path.GetFileName(m.Groups[2].Value.Trim().TrimStart('*')) == filename)
                ?.Groups[1].Value.ToLowerInvariant();
            if (expected is null)
                throw new InvalidOperationException("Official SHA-256 checksum unavailable for " + filename);
            async Task<string> HashFile(string path)
            {
                await using var file = File.OpenRead(path);
                return Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
            }
            void Verified(string actual)
            {
                if (kali)
                {
                    _kaliVerified = true;
                    _kaliSha256 = actual;
                    if (_kaliHashLabel is not null)
                    {
                        _kaliHashLabel.Text = "SHA-256: " + actual[..12] + "…";

                        ToolTip.SetTip(_kaliHashLabel, actual);
                    }
                }
                if (kali && _attackerCopyButton is not null) _attackerCopyButton.IsEnabled = true;
                if (!kali && _targetCopyButton is not null) _targetCopyButton.IsEnabled = true;
                if (!kali) { _debianVerified = true; _debianSha256 = actual;
                    if (_debianHashLabel is not null) { _debianHashLabel.Text = "SHA-256: " + actual[..12] + "…"; ToolTip.SetTip(_debianHashLabel, actual); }
                }
                Status(kali ? "Kali image verified - ready to register VM" :
                    "Debian ISO verified - automated installation pending", 100);
                if (actionButton is not null)
                {
                    actionButton.Content = kali ? "Kali downloaded" : "Debian downloaded";
                    actionButton.IsEnabled = true;
                    actionButton.BorderBrush = Paint(Green);
                }
            }
            if (File.Exists(destination))
            {
                Status("Verifying existing image...", 0);
                var existingHash = await HashFile(destination);
                if (string.Equals(existingHash, expected, StringComparison.OrdinalIgnoreCase))
                {
                    if (kali) _verifiedKaliPath = destination;
                    Verified(existingHash);
                    return;
                }
                Status("Existing image differs from official SHA-256; downloading...", 0);
            }
            Status("Downloading 0%", 0);
            using var response = await http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var length = response.Content.Headers.ContentLength;
            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
            {
                var buffer = new byte[1024 * 1024];
                long received = 0;
                int count;
                while ((count = await input.ReadAsync(buffer)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, count));
                    received += count;
                    var percent = length > 0 ? Math.Min(99, received * 100d / length.Value) : 0;
                    Status(length > 0 ? $"Downloading {percent:F0}%" : $"Downloading {received / 1048576d:F0} MB", percent);
                }
            }
            File.Move(temporary, destination, true);
            Status("Verifying SHA-256...", 99);
            var downloadedHash = await HashFile(destination);
            if (!string.Equals(downloadedHash, expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(destination);
                throw new InvalidOperationException("SHA-256 mismatch. Download removed for safety.");
            }
            if (kali) _verifiedKaliPath = destination;
            Verified(downloadedHash);
        }
        catch (Exception ex)
        {
            Status("Download unavailable: " + ex.Message, 0);
        }
        finally
        {
            if (kali) _downloadingKali = false; else _downloadingDebian = false;
            if (actionButton is not null)
                actionButton.IsEnabled = File.Exists(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Oracle", "VirtualBox", "VBoxManage.exe"));
            if (kali && _installAttackerButton is not null)
                _installAttackerButton.IsEnabled = _kaliVerified && !_installingAttacker;
        }
    }

    private void OpenAttackerIsoPage()
    {
        // Official prebuilt VirtualBox image; no installer ISO needed.
        OpenDownloadPage("https://www.kali.org/get-kali/#kali-virtual-machines", "Kali Linux VirtualBox image");
    }

    private void OpenAcademyDebianReleases()
    {
        // No Academy-customized VM image is published yet. Do not substitute stock Debian.
        OpenDownloadPage("https://github.com/Marcope113/ai-secure-hydration-kit/releases",
            "Academy Debian VM image (release pending)");
    }

    private void OpenDownloadPage(string url, string label)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            _activity.Text = label + ": opened download page in browser. Verify the publisher and SHA-256 before use.";
        }
        catch (Exception ex)
        {
            _activity.Text = "Unable to open " + label + ": " + ex.Message + "  URL: " + url;
        }
    }

    private Control TargetControls(bool hypervisorReady)
    {
        var stack = Stack(3);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        var getDebian = Button("Get Debian ISO", () => { if (_debianVerified) OpenIsoFolder(); else _ = DownloadLabImageAsync("debian"); });
        getDebian.IsEnabled = hypervisorReady && !_downloadingDebian;
        if (_debianVerified) { getDebian.Content = "Debian downloaded"; getDebian.BorderBrush = Paint(Green); }
        _debianDownloadButton = getDebian;
        actions.Children.Add(getDebian);
        actions.Children.Add(Txt("→", 19, Accent, FontWeight.Bold));
        actions.Children.Add(PendingInstall("Install Target"));
        stack.Children.Add(actions);
        return stack;
    }

    private static Button PendingInstall(string title) =>
        new() {
            Content = title,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(9),
            Background = Paint("#142B42"),
            BorderBrush = Paint("#53DCE8"),
            BorderThickness = new Thickness(1.5),
            Foreground = Paint(Muted),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

    private Border Requirement(string title, string detail, string status, string statusColor, string manual, string? installLabel)
    {
        var body = Stack(5);
        var iconKind = title switch {
            "OPERATING SYSTEM" => "windows",
            "ARCHITECTURE" => "cpu",
            "DISK SPACE" => "disk",
            "PHYSICAL RAM" => "ram",
            "LOGICAL PROCESSORS" => "cpu",
            _ => "computer"
        };
        body.Children.Add(new IsometricLabIcon { Kind = iconKind, HorizontalAlignment = HorizontalAlignment.Center });
        foreach (var line in new[] {
            Txt(title, 12, Muted, FontWeight.Bold),
            Txt(detail, 18, Text, FontWeight.SemiBold),
            Txt(status, 13, statusColor, FontWeight.Bold)
        })
        {
            line.TextAlignment = TextAlignment.Center;
            line.HorizontalAlignment = HorizontalAlignment.Stretch;
            body.Children.Add(line);
        }
        if (title == "VIRTUALBOX" && installLabel is null)
        {
            body.Children.Add(Txt("READY  /  No installation needed", 12, Green, FontWeight.SemiBold));
        }
        if (installLabel is not null)
        {
            if (title == "VIRTUALBOX")
                body.Children.Add(Button(_installingVirtualBox ? "Installing..." : "Install", () => _ = InstallVirtualBoxAsync()));
            body.Children.Add(Button("Manual instructions", () => ShowInstructions(title, manual)));
        }
        return Tile(body);
    }

    private Window ShowVirtualBoxProgress(string action)
    {
        var progressWindow = new Window {
            Title = "VirtualBox - " + action, Width = 490, Height = 205,
            Background = Paint("#152332"), WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };
        var content = Stack(14);
        content.Margin = new Thickness(24);
        content.Children.Add(Txt(action + " VirtualBox", 19, Text, FontWeight.Bold));
        content.Children.Add(Txt("Preparing winget / waiting for Windows installer. This can take several minutes.", 13, Muted));
        content.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 10 });
        content.Children.Add(Txt("Working... Please wait for completion or a Windows permission prompt.", 12, Accent));
        progressWindow.Content = content;
        progressWindow.Show(this);
        return progressWindow;
    }

    private async Task UninstallVirtualBoxAsync()
    {
        if (_removingVirtualBox || _installingVirtualBox) return;
        var vboxManage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Oracle", "VirtualBox", "VBoxManage.exe");
        Window? progressWindow = null;
        try
        {
            if (!File.Exists(vboxManage))
            {
                ShowSetup();
                return;
            }
            var check = new ProcessStartInfo(vboxManage) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            check.ArgumentList.Add("list");
            check.ArgumentList.Add("runningvms");
            using (var p = Process.Start(check) ?? throw new InvalidOperationException("Cannot inspect running VMs."))
            {
                var outputTask = p.StandardOutput.ReadToEndAsync();
                var errorTask = p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();
                var output = await outputTask;
                var error = await errorTask;
                if (p.ExitCode != 0) throw new InvalidOperationException("Cannot verify VM state: " + error);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    ShowInstructions("Uninstall blocked", "Shut down all running VirtualBox VMs before uninstalling. No changes were made.");
                    return;
                }
            }
            var dialog = new Window {
                Title = "Confirm VirtualBox removal", Width = 560, Height = 260,
                Background = Paint("#152332"), WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var panel = Stack(12);
            panel.Margin = new Thickness(22);
            panel.Children.Add(Txt("Uninstall Oracle VirtualBox from this Windows host?", 16, Text));
            panel.Children.Add(Txt("This removes the hypervisor and may disrupt your lab. The app will not delete VM files, but the uninstaller's behavior is outside its control. Back up your VMs first.", 12, Yellow));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            buttons.Children.Add(Button("Cancel", () => dialog.Close(false)));
            buttons.Children.Add(Button("Uninstall VirtualBox", () => dialog.Close(true)));
            panel.Children.Add(buttons);
            dialog.Content = panel;
            if (!await dialog.ShowDialog<bool>(this)) return;

            _removingVirtualBox = true;
            _activity.Text = "Uninstalling VirtualBox using winget...";
            progressWindow = ShowVirtualBoxProgress("Uninstalling");
            var start = new ProcessStartInfo("winget.exe") {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] { "uninstall", "--id", "Oracle.VirtualBox", "--exact",
                "--source", "winget", "--accept-source-agreements" })
                start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start winget.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var outputText = await stdoutTask;
            var errorText = await stderrTask;
            var logDir = Path.Combine(_root, "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "virtualbox-uninstall.log");
            await File.WriteAllTextAsync(logPath, outputText + Environment.NewLine + errorText);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"winget exited with code {process.ExitCode}. Log: {logPath}");
            _activity.Text = "VirtualBox removal completed. Checking installation status...";
        }
        catch (Exception ex)
        {
            _activity.Text = "VirtualBox removal failed: " + ex.Message;
            ShowInstructions("VirtualBox uninstall failed", ex.Message);
        }
        finally
        {
            progressWindow?.Close();
            _removingVirtualBox = false;
            ShowSetup();
        }
    }

    private async Task InstallVirtualBoxAsync()
    {
        if (_installingVirtualBox) return;
        var dialog = new Window {
            Title = "Confirm VirtualBox installation", Width = 500, Height = 220,
            Background = Paint("#152332"), WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var panel = Stack(12);
        panel.Margin = new Thickness(22);
        panel.Children.Add(Txt("Install Oracle VirtualBox using Windows Package Manager (winget)?", 16, Text));
        panel.Children.Add(Txt("This modifies Windows and may request administrator approval.", 12, Muted));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        buttons.Children.Add(Button("Cancel", () => dialog.Close(false)));
        buttons.Children.Add(Button("Install", () => dialog.Close(true)));
        panel.Children.Add(buttons);
        dialog.Content = panel;
        if (!await dialog.ShowDialog<bool>(this)) return;

        _installingVirtualBox = true;
        _activity.Text = "Installing VirtualBox via winget. Please wait...";
        var progressWindow = ShowVirtualBoxProgress("Installing");
        try
        {
            var start = new ProcessStartInfo("winget.exe") {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in new[] {
                "install", "--id", "Oracle.VirtualBox", "--exact", "--source", "winget",
                "--accept-source-agreements", "--accept-package-agreements"
            }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start winget.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await stdout;
            var error = await stderr;
            var logDir = Path.Combine(_root, "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "virtualbox-install.log");
            await File.WriteAllTextAsync(logPath, output + Environment.NewLine + error);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"winget exited with code {process.ExitCode}. Log: {logPath}");
            _activity.Text = "VirtualBox installation completed. Refreshing requirements.";
        }
        catch (Exception ex)
        {
            _activity.Text = "VirtualBox installation failed: " + ex.Message;
            ShowInstructions("VirtualBox installation failed",
                "Install manually from https://www.virtualbox.org/wiki/Downloads and reopen the app. " +
                "Error: " + ex.Message);
        }
        finally
        {
            progressWindow.Close();
            _installingVirtualBox = false;
            // Refresh the requirement tiles after the process finishes.
            ShowSetup();
        }
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
            // Update the current topology in place. Do not rebuild the page.
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
