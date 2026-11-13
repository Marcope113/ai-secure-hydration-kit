# Architecture (pre-release)

- Private repository: development and training scenarios.
- Public repository: reviewed launcher and release metadata.
- VM images: immutable, versioned artifacts with verified checksums and provenance.

## Planned commands

`Check`, `Install`, `Update`, `Status`, `Reset`, `Uninstall` — only **Check** and **Version** are implemented now.

## Safety gates

A vulnerable VM must remain off the household LAN. Avoid NAT and bridged networking after provisioning. Updates require user consent and verification; don't execute mutable scripts from `main` automatically. All modifications must be logged in an ownership manifest and cleanup must be restricted to owned resources.
