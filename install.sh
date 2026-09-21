#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

INSTALL_DIR="$HOME/.local/share/discord-overlay"
BIN_DIR="$HOME/.local/bin"
APPS_DIR="$HOME/.local/share/applications"
LAUNCHER="$BIN_DIR/discord-overlay"

if [ ! -f dist/DiscordOverlay ]; then
    ./publish.sh
fi

mkdir -p "$INSTALL_DIR" "$BIN_DIR" "$APPS_DIR"

cp dist/DiscordOverlay "$INSTALL_DIR/DiscordOverlay"
chmod +x "$INSTALL_DIR/DiscordOverlay"
cp DiscordOverlay/Assets/tray-icon.png "$INSTALL_DIR/icon.png"
cp DiscordOverlay/config.example.json "$INSTALL_DIR/config.example.json"

if [ -f DiscordOverlay/config.json ]; then
    cp DiscordOverlay/config.json "$INSTALL_DIR/config.json"
    chmod 600 "$INSTALL_DIR/config.json"
fi

cat > "$LAUNCHER" <<LAUNCH
#!/usr/bin/env bash
cd "$INSTALL_DIR"
exec "$INSTALL_DIR/DiscordOverlay" "\$@"
LAUNCH
chmod +x "$LAUNCHER"

cat > "$APPS_DIR/discord-overlay.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Discord Overlay
Comment=Shows who is in your Discord voice channel and who is talking
Exec=$LAUNCHER
Icon=$INSTALL_DIR/icon.png
Terminal=false
Categories=Utility;Network;
DESKTOP

echo
echo "Installed to $INSTALL_DIR"
echo "Start it from your app menu (Discord Overlay) or by running: discord-overlay"

if [ ! -f "$INSTALL_DIR/config.json" ]; then
    echo
    echo "No config.json found. Create one before starting the overlay:"
    echo "  cp $INSTALL_DIR/config.example.json $INSTALL_DIR/config.json"
    echo "  nano $INSTALL_DIR/config.json"
fi
