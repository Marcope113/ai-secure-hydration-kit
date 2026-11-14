#!/usr/bin/env bash
# Bake VirtualBox integration into a Kali desktop or Debian headless golden image.
set -euo pipefail
if (( EUID != 0 )); then echo "Run with sudo" >&2; exit 1; fi
command -v apt-get >/dev/null || { echo "Requires apt-get" >&2; exit 1; }
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y virtualbox-guest-utils
# The Debian target is headless: installing X11 integration would not enable
# clipboard sharing in its text-only VirtualBox console.
if command -v Xorg >/dev/null || command -v Xwayland >/dev/null || dpkg-query -W -f='${Status}' kali-desktop-xfce 2>/dev/null | grep -q 'install ok installed'; then
  apt-get install -y virtualbox-guest-x11
  echo "Desktop detected: clipboard integration installed (requires graphical login)."
else
  echo "Headless guest: vbox guest service installed; use SSH for terminal copy/paste."
fi
systemctl enable vboxservice.service 2>/dev/null || true
echo "Reboot and verify guest services before exporting golden image."
