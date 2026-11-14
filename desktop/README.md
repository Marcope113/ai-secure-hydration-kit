# Hydration Kit Desktop - Windows preview

An initial Avalonia UI desktop application for Windows. This is a **source-only, untested prototype**; the PowerShell launcher remains the supported development path.

## Prerequisites

- Windows 11 x64
- .NET 8 SDK (the developer bootstrap offers to install it using winget with permission)
- Git

## One-command developer bootstrap

From the repository root, run:

```powershell
.\start-hydration.ps1
```

This detects .NET 8 SDK, requests consent to install it if absent, restores Avalonia dependencies, and launches the desktop application. It does not install VMs or guest operating systems. For read-only dependency checks, use `.\start-hydration.ps1 -CheckOnly`.

## Run manually from the repository root

```powershell
dotnet restore .\desktop\AISecure.Hydration.Desktop\AISecure.Hydration.Desktop.csproj
dotnet run --project .\desktop\AISecure.Hydration.Desktop\AISecure.Hydration.Desktop.csproj
```

The application detects host OS/CPU architecture, available disk space and VirtualBox executable, and reads the existing Academy manifests. It allows saving a Kali or Parrot preference in the same JSON file as the PowerShell CLI. It does **not** install VirtualBox, download an ISO, create VMs or install Debian. Memory and virtualization capability checks are not yet integrated into the desktop app.

## Next development stages

- Build and test on Windows 11
- Add comprehensive host requirements detection
- Introduce a typed, testable provisioning engine and real operation progress events
- Integrate safe, consent-driven VirtualBox and ISO operations
- Ship signed, versioned Windows binaries
- Add Linux and macOS adapters only after host compatibility tests
