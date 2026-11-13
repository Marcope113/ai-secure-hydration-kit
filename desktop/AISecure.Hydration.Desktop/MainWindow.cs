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
    private ComboBox _distro = new() { ItemsSource = new[] { "Kali Linux", "Parrot Security OS" }, SelectedIndex = 0, MinWidth = 210 };
    private TextBlock _activity = Txt("Waiting for diagnostics.", 13, Muted);
    private Button _next = new() { Content = "Next: Learning Path (verification pending)", IsEnabled = false, Padding = new Thickness(18, 12) };
    private string _selected = "kali";
    private bool _installingVirtualBox;
    private bool _removingVirtualBox;
    private readonly string _isoRoot = ResolveIsoDirectory();
    private TextBlock _attackerProgressText = Txt("Waiting to install", 11, Muted);
    private TextBlock _targetProgressText = Txt("Waiting to install", 11, Muted);
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
        new() { Background = Paint(background), CornerRadius = new CornerRadius(12), Padding = new Thickness(19), Child = child };
    private static StackPanel Stack(int gap = 9) => new() { Spacing = gap };
    private static Button Button(string title, Action click)
    {
        var b = new Button { Content = title, Padding = new Thickness(14, 9), HorizontalAlignment = HorizontalAlignment.Left };
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

    private static string ResolveIsoDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("AISECURE_KIT_ROOT");
        var root = !string.IsNullOrWhiteSpace(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return Path.Combine(root, "downloads", "isos");
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
        body.Children.Add(Txt(details, 16, Muted));
        if (action is not null) body.Children.Add(action);
        return Tile(body);
    }

    private void ShowSetup()
    {
        // Progress is model state; controls belong to one page instance only.
        // Never reparent an existing TextBlock or ProgressBar during Refresh.
        _attackerProgressText = Txt("Waiting to install", 11, Muted);
        _targetProgressText = Txt("Waiting to install", 11, Muted);
        _attackerProgress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 7 };
        _targetProgress = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 7 };
        // Create fresh controls for each page build. Avalonia controls cannot
        // be attached to multiple logical parents after a rebuild.
        _distro = new ComboBox { ItemsSource = new[] { "Kali Linux" }, SelectedIndex = 0, MinWidth = 210 };
        _attackerOs = Txt("Kali Linux", 22, Text, FontWeight.SemiBold);
        _activity = Txt("Ready. No VM changes made.", 13, Muted);
        _next = new Button { Content = "Next: Learning Path (verification pending)", IsEnabled = false, Padding = new Thickness(18, 12) };
        _next.Click += (_, _) => ShowLessons();

        Header("LAB SETUP  /  STEP 1 OF 2");
        var overview = Stack(8);
        overview.Children.Add(Txt("Linux PrivEsc Techniques", 28, Text, FontWeight.Bold));
        overview.Children.Add(Txt("Explore how Linux privilege boundaries fail. Enumerate misconfigurations, gain elevated access in an isolated environment, then investigate detection and remediation.", 14, Muted));
        overview.Children.Add(Txt("Kali Linux attacker + Academy Debian vulnerable target, both on VirtualBox.", 13, Accent));
        _page.Children.Add(Tile(overview));

        Section("VIRTUAL LAB ARCHITECTURE");
        var topology = Stack(12);
        topology.Children.Add(Txt("Isolated Linux privilege escalation environment", 13, Muted));
        var layers = new Grid {
            ColumnDefinitions = new ColumnDefinitions("*,170"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 16, RowSpacing = 12
        };
        Control LayerLabel(string number, string title, string description, string color)
        {
            var label = Stack(5);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Children.Add(Txt(number, 12, color, FontWeight.Bold));
            label.Children.Add(Txt(title, 16, Text, FontWeight.SemiBold));
            label.Children.Add(Txt(description, 12, Muted));
            return label;
        }
        void AddLayer(Control label, Control content, int row)
        {
            Grid.SetRow(content, row); Grid.SetColumn(content, 0); layers.Children.Add(content);
            Grid.SetRow(label, row); Grid.SetColumn(label, 1); layers.Children.Add(label);
        }
        var network = new Grid { ColumnDefinitions = new ColumnDefinitions("*,150,*"), ColumnSpacing = 12 };
        var attackerControls = Stack(8);
        attackerControls.Children.Add(Button("Get Kali VirtualBox Image", OpenAttackerIsoPage));
        attackerControls.Children.Add(PendingInstall("Install Attacker"));
        attackerControls.Children.Add(_attackerProgressText);
        attackerControls.Children.Add(_attackerProgress);
        var attackerBody = Stack(10);
        var attackerHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var attackerRole = Txt("[>_]   ATTACKER", 15, Yellow, FontWeight.Bold);
        Grid.SetColumn(attackerRole, 0);
        attackerHeader.Children.Add(attackerRole);
        _distro.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_distro, 1);
        // Fixed attacker distribution: no selection required.
        attackerBody.Children.Add(attackerHeader);
        attackerBody.Children.Add(_attackerOs);
        attackerBody.Children.Add(Txt("NOT INSTALLED", 13, Yellow, FontWeight.Bold));
        attackerBody.Children.Add(Txt("PLANNED IP   10.4.4.10\nOS VERSION   Not installed", 16, Muted));
        attackerBody.Children.Add(attackerControls);
        var attacker = Tile(attackerBody);
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
        link.Children.Add(new Button {
            Content = "Configure",
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(10, 8)
        });
        var linkTile = new Border {
            Background = Paint("#24394D"), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 14), VerticalAlignment = VerticalAlignment.Center,
            Child = link
        };
        Grid.SetColumn(linkTile, 1); network.Children.Add(linkTile);
        var target = Machine("[#]", "VICTIM / TARGET", "Debian 13",
            Existing("debian-media.json") == "Found" ? "ISO ATTACHED" : "NOT PREPARED", Purple,
            "PLANNED IP   10.4.4.20\nOS VERSION   Debian 13 (not installed)\nMEDIA   Debian 13.7.0 ISO",
            TargetControls());
        Grid.SetColumn(target, 2); network.Children.Add(target);
        AddLayer(LayerLabel("LAYER 03", "Virtual Machines", "Guests and isolated network", Yellow), network, 0);

        // The hypervisor is the foundation beneath both guest machines.
        var hypervisorPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Oracle", "VirtualBox", "VBoxManage.exe");
        var hypervisorReady = File.Exists(hypervisorPath);
        var foundationInfo = Stack(7);
        foundationInfo.HorizontalAlignment = HorizontalAlignment.Stretch;
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
        if (hypervisorReady)
        {
            var uninstallHypervisor = Button("Uninstall VirtualBox", () => _ = UninstallVirtualBoxAsync());
            uninstallHypervisor.HorizontalAlignment = HorizontalAlignment.Center;
            foundationInfo.Children.Add(uninstallHypervisor);
        }
        else
        {
            var installHypervisor = Button("Install VirtualBox", () => _ = InstallVirtualBoxAsync());
            installHypervisor.HorizontalAlignment = HorizontalAlignment.Center;
            foundationInfo.Children.Add(installHypervisor);
        }
        AddLayer(LayerLabel("LAYER 02", "Hypervisor", "Virtualization platform", Accent), Tile(foundationInfo, "#20384A"), 1);
        // Physical host is the bottom layer of the topology, beneath VirtualBox.
        // Host hardware and OS are the physical foundation of the VM stack.
        var build = Environment.OSVersion.Version.Build;
        var windows = OperatingSystem.IsWindows();
        var architecture = RuntimeInformation.OSArchitecture;
        var archOk = windows && architecture == Architecture.X64 && build >= 22000;
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var free = drive.AvailableFreeSpace / 1073741824d;
        var diskOk = free >= 35;
        var physicalHost = Stack(10);
        var hostTitle = Txt("HOST MACHINE  /  " +
            (windows && build >= 22000 ? "Windows 11" : RuntimeInformation.OSDescription), 15, Accent, FontWeight.Bold);
        hostTitle.TextAlignment = TextAlignment.Center;
        hostTitle.HorizontalAlignment = HorizontalAlignment.Stretch;
        physicalHost.Children.Add(hostTitle);
        var hostSpecs = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 12 };
        var architectureTile = Requirement("ARCHITECTURE", architecture.ToString(), State(archOk), archOk ? Green : Red,
            "Windows 11 x64 is required for this Windows preview.", null);
        var diskTile = Requirement("DISK SPACE", $"{free:F1} GB free", diskOk ? "SUPPORTED" : "NOT ENOUGH SPACE", diskOk ? Green : Red,
            "Minimum 35 GB free for the initial target VM. More space is recommended for two VMs.", null);
        Grid.SetColumn(architectureTile, 0); hostSpecs.Children.Add(architectureTile);
        Grid.SetColumn(diskTile, 1); hostSpecs.Children.Add(diskTile);
        var physicalRam = GetPhysicalRamGiB();
        var ramOk = physicalRam is >= 8;
        var ramTile = Requirement("PHYSICAL RAM",
            physicalRam is null ? "Unavailable" : $"{physicalRam.Value:F1} GB total",
            physicalRam is null ? "CHECK UNAVAILABLE" : ramOk ? "SUPPORTED" : "8 GB RECOMMENDED",
            physicalRam is null ? Yellow : ramOk ? Green : Yellow,
            "8 GB or more is recommended for running Kali and Debian together.", null);
        Grid.SetColumn(ramTile, 2); hostSpecs.Children.Add(ramTile);
        physicalHost.Children.Add(hostSpecs);
        var pendingHostChecks = Txt("CPU virtualization: verification pending", 12, Muted);
        pendingHostChecks.TextAlignment = TextAlignment.Center;
        pendingHostChecks.HorizontalAlignment = HorizontalAlignment.Stretch;
        physicalHost.Children.Add(pendingHostChecks);
        AddLayer(LayerLabel("LAYER 01", "Physical Host", "Hardware and operating system", Muted),
            Tile(physicalHost, "#1B3042"), 2);
        topology.Children.Add(layers);
        var flowNote = Txt("SETUP FLOW   Host  →  Hypervisor  →  Virtual Machines  →  Network validation", 12, Accent);
        flowNote.TextAlignment = TextAlignment.Center;
        topology.Children.Add(flowNote);
        _page.Children.Add(new Border {
            Background = Paint("#0F2030"), CornerRadius = new CornerRadius(16),
            BorderBrush = Paint("#294153"), BorderThickness = new Thickness(1),
            Padding = new Thickness(18), Child = topology
        });

        var isoLocation = Stack(7);
        isoLocation.Children.Add(Txt("ISO STORAGE  /  HYDRATION KIT", 13, Accent, FontWeight.Bold));
        isoLocation.Children.Add(Txt(_isoRoot, 13, Text));
        isoLocation.Children.Add(Button("Open ISO Folder", OpenIsoFolder));
        _page.Children.Add(Tile(isoLocation, "#122432"));

        _page.Children.Add(Txt("IPs are planned static addresses (10.4.4.0/24). ACTIVE will require both guests to be verified, their interfaces configured and bidirectional connectivity tested.", 12, Muted));

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

        _page.Children.Add(Button("Refresh diagnostics", ShowSetup));
        Section("ACTIVITY");
        _page.Children.Add(_activity);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 20, 0, 12) };
        var nextInfo = Txt("STEP 2 OF 2  /  LEARNING PATH  -  Available after lab validation", 12, Muted);
        nextInfo.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(nextInfo, 0);
        footer.Children.Add(nextInfo);
        _next.Content = "Next  >";
        _next.IsEnabled = false;
        _next.MinWidth = 155;
        _next.HorizontalAlignment = HorizontalAlignment.Right;
        _next.Background = Paint("#176B80");
        _next.Foreground = Paint("#F0F5FA");
        _next.CornerRadius = new CornerRadius(9);
        _next.FontWeight = FontWeight.SemiBold;
        Grid.SetColumn(_next, 1);
        footer.Children.Add(_next);
        _page.Children.Add(footer);
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

    private Control TargetControls()
    {
        var stack = Stack(8);
        stack.Children.Add(Button("Get Academy Debian VM", OpenAcademyDebianReleases));
        stack.Children.Add(PendingInstall("Install Target"));
        stack.Children.Add(_targetProgressText);
        stack.Children.Add(_targetProgress);
        return stack;
    }

    private static Button PendingInstall(string title) =>
        new() {
            Content = title + " (pending validation)",
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 8)
        };

    private Border Requirement(string title, string detail, string status, string statusColor, string manual, string? installLabel)
    {
        var body = Stack(8);
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
