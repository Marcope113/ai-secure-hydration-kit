# Historical Hydration scripts (v0.1–v0.3)

These files are preserved from the Academy development branches for reference and reproducibility. **They are not the supported student installer.** They have not been re-tested as part of this public release.

- `v01/hydration.ps1`: host prerequisites and VirtualBox installation (may modify the host).
- `v02/hydration-v02.ps1`: creates an empty VM (originally configured as Ubuntu_64).
- `v03/hydration-v03.ps1`: downloads and attaches an **Ubuntu** ISO, which is now obsolete for our Debian-based lab.

**Do not run these scripts in sequence on a machine already configured with a Hydration VM.** They maintain separate state files and may refuse to proceed or make unwanted changes. The v0.3 ISO is multi-GB and not part of the current Debian plan.

Use the repository root `aisecure.ps1 -Action Check` for safe diagnostics. The integrated Debian workflow will be promoted separately after testing and review.
