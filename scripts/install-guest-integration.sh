#!/usr/bin/env bash
# Run when building the Kali/Debian golden image, before exporting the OVA.
set -euo pipefail
if [[ $(id -u) -ne 0 ]]; then echo "Run as root: sudo bash $0" >&2; exit 1; fi
if ! command -v apt-get >/dev/null; then echo "Only Debian-family guests supported" >&2; exit 1; fi
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y virtualbox-guest-utils virtualbox-guest-x11
systemctl enable vboxservice.service 2>/dev/null || true
echo "Guest integration packages installed. Reboot, log into the desktop and validate VBoxClient before exporting the image."
