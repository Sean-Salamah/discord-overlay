#!/usr/bin/env bash
set -euo pipefail

rm -rf "$HOME/.local/share/discord-overlay"
rm -f "$HOME/.local/bin/discord-overlay"
rm -f "$HOME/.local/share/applications/discord-overlay.desktop"
rm -f "$HOME/.config/autostart/discord-overlay.desktop"

echo "Discord Overlay removed."
echo "Your saved Discord login is in ~/.config/discord-overlay. Delete that folder too if you want to remove it."
