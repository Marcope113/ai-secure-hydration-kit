# Hydration Kit Desktop - Windows preview

An initial Avalonia UI desktop application for Windows. This is a **source-only, untested prototype**; the PowerShell launcher remains the supported development path.

## Prerequisites

- Windows 11 x64
- .NET 8 SDK (install separately from Microsoft if missing)
- Git

## Run from the repository root

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
