using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AISecure.Hydration.Desktop;

/// <summary>
/// Temporary Debian ISO-based unattended lab target. Replaced later by an Academy VM image.
/// Never modifies or deletes an existing VM; installation state is confirmed through VirtualBox.
/// </summary>
internal static class DebianLabProvisioner
{
    internal const string Name = "AISecure-Debian-Lab";
    internal static string VBoxPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "Oracle", "VirtualBox", "VBoxManage.exe");

    internal static async Task<string> RunAsync(TimeSpan timeout, params string[] args)
    {
        var psi = new ProcessStartInfo(VBoxPath) {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("VBoxManage could not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(timeout);
        try { await process.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("VBoxManage timed out: " + string.Join(" ", args.Take(2)));
        }
        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode != 0)
            throw new InvalidOperationException("VBoxManage " + string.Join(" ", args.Take(2)) +
                " failed: " + error + " " + output);
        return output;
    }

    internal static async Task<string> FindVmAsync()
    {
        var list = await RunAsync(TimeSpan.FromSeconds(20), "list", "vms");
        var match = Regex.Match(list, @"(?m)^""AISecure-Debian-Lab""\s+\{([a-fA-F0-9-]+)\}");
        return match.Success ? match.Groups[1].Value : "";
    }

    internal static async Task<string> EnsureInstalledAsync(string isoPath, int memoryMb, int cpus,
        int diskGb, Action<string> report)
    {
        if (!File.Exists(isoPath)) throw new FileNotFoundException("Verified Debian ISO missing.", isoPath);
        if (!File.Exists(VBoxPath)) throw new FileNotFoundException("VirtualBox is not installed.", VBoxPath);
        var uuid = await FindVmAsync();
        if (uuid.Length == 0)
        {
            report("Creating Debian virtual machine...");
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AISecureAcademy", "virtual-machines", "debian");
            Directory.CreateDirectory(root);
            await RunAsync(TimeSpan.FromSeconds(30), "createvm", "--name", Name,
                "--ostype", "Debian_64", "--basefolder", root, "--register");
            uuid = await FindVmAsync();
            if (uuid.Length == 0) throw new InvalidOperationException("Debian VM registration failed.");
            await RunAsync(TimeSpan.FromSeconds(20), "modifyvm", uuid,
                "--memory", memoryMb.ToString(), "--cpus", cpus.ToString(),
                "--nic1", "intnet", "--intnet1", "aisecurelab", "--cableconnected1", "on",
                "--nic2", "none", "--boot1", "disk", "--boot2", "dvd");
            await RunAsync(TimeSpan.FromSeconds(20), "storagectl", uuid,
                "--name", "SATA", "--add", "sata", "--controller", "IntelAhci");
            var disk = Path.Combine(root, "academy-debian-target.vdi");
            await RunAsync(TimeSpan.FromSeconds(90), "createmedium", "disk",
                "--filename", disk, "--size", (diskGb * 1024).ToString(), "--format", "VDI");
            await RunAsync(TimeSpan.FromSeconds(20), "storageattach", uuid,
                "--storagectl", "SATA", "--port", "0", "--device", "0",
                "--type", "hdd", "--medium", disk);
            report("Preparing unattended Debian installer...");
            await RunAsync(TimeSpan.FromMinutes(3), "unattended", "install", uuid,
                "--iso=" + isoPath,
                "--user=debian", "--password=debian",
                "--full-user-name=Academy Student",
                "--hostname=debian.aisecurelab.test",
                "--locale=en_US", "--country=US",
                "--time-zone=Australia/Sydney",
                "--start-vm=gui");
        }
        else
        {
            report("Existing Academy Debian VM found; preserving its disk and configuration.");
            var info = await RunAsync(TimeSpan.FromSeconds(20), "showvminfo", uuid, "--machinereadable");
            if (info.Contains("VMState=\"poweroff\"", StringComparison.OrdinalIgnoreCase))
                await RunAsync(TimeSpan.FromSeconds(30), "startvm", uuid, "--type", "gui");
        }
        return uuid;
    }

    // A registered VM or DHCP lease alone does not prove that Debian finished installing.
    // Require a running guest reporting Debian through VirtualBox Guest Additions.
    internal static async Task<bool> IsInstalledGuestRunningAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid)) return false;
        var info = await RunAsync(TimeSpan.FromSeconds(20),
            "showvminfo", uuid, "--machinereadable");
        if (!info.Contains("VMState=\"running\"", StringComparison.OrdinalIgnoreCase))
            return false;
        var properties = await RunAsync(TimeSpan.FromSeconds(20),
            "guestproperty", "enumerate", uuid);
        return Regex.IsMatch(properties,
            @"(?im)^Name:\s*/VirtualBox/GuestInfo/OS/Product,\s*value:\s*Debian(?:\s|,|$)");
    }

    internal static async Task<string?> FindDhcpAddressAsync(string uuid)
    {
        var info = await RunAsync(TimeSpan.FromSeconds(20), "showvminfo", uuid, "--machinereadable");
        var mac = Regex.Match(info, @"(?im)^macaddress1=""?([a-f0-9]{12})""?");
        if (!mac.Success) return null;
        var address = string.Join(":", Enumerable.Range(0, 6)
            .Select(i => mac.Groups[1].Value.Substring(i * 2, 2))).ToLowerInvariant();
        var lease = await RunAsync(TimeSpan.FromSeconds(20), "dhcpserver", "findlease",
            "--network", "aisecurelab", "--mac-address", address);
        var ip = Regex.Match(lease, @"\b10\.4\.4\.(?:100|101)\b");
        return ip.Success ? ip.Value : null;
    }
}
