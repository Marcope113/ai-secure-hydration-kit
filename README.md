# AI Secure Academy — Hydration Kit

Public Windows bootstrap for reproducible cybersecurity training labs.

> **Preview:** Consolidates the original v0.1–v0.3 host setup, VM creation and Debian media preparation. **Does not install the Debian guest, download an attacker ISO, or deploy vulnerable configurations.** This combined version has not yet been tested end-to-end on a clean Windows host.

## First time

```powershell
git clone https://github.com/Marcope113/ai-secure-hydration-kit.git
cd ai-secure-hydration-kit
.\aisecure.ps1 -Action Check
```

## Updating

```powershell
git pull --ff-only
.\aisecure.ps1 -Action Check
```

## Available actions

| Action | What it does |
|---|---|
| `Check` | Read-only Windows, VM prerequisites and manifest diagnostics |
| `Version` | Print launcher version |
| `Status` | Display existing v0.1/v0.2/Debian media state |
| `Install` | Install VirtualBox through winget **with explicit consent**, if missing |
| `CreateVM` | Create an **empty** Debian VirtualBox VM with isolated internal networking |
| `SelectAttacker` | Choose Kali Linux or Parrot Security OS and save the choice (no download yet) |
| `PrepareMedia` | Reuse or download pinned Debian 13.7.0 ISO, verify SHA-256, attach to powered-off VM |

The launcher uses native PowerShell `Write-Progress` for animated step-by-step status, and curl's native download progress. Long-running VirtualBox commands may not report fractional progress.

**Important:** If you've already created the VM using v0.2, do not run `CreateVM` again. Use `Status` first. If you've already downloaded the Debian ISO, `PrepareMedia` reuses it.

This preview pins Debian 13.7.0 by filename and known SHA-256. A future production release will verify signed release metadata and deliver a tested OVA.

## Legacy

Historical scripts are in [legacy](legacy/README.md), including the obsolete Ubuntu-based v0.3. They are not the recommended launcher.

## Security

Do not connect deliberately vulnerable lab machines to bridged or NAT networks after provisioning. The public launcher never installs a vulnerable guest. Review and approve every modifying operation.

MIT License.

## Attacker VM roadmap

Run `./aisecure.ps1 -Action SelectAttacker` (PowerShell syntax: `.\\aisecure.ps1 -Action SelectAttacker`) to choose Kali Linux or Parrot Security OS. The selection is stored in `%LOCALAPPDATA%\\AISecureAcademy\\attacker-selection.json`. ISO retrieval, verified official release metadata, unattended guest installation, and VM creation are **not yet implemented**; they will be promoted only after release verification and testing.
