#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

dotnet publish DiscordOverlay/DiscordOverlay.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -o dist

rm -f dist/config.json
cp DiscordOverlay/config.example.json dist/config.example.json

echo
echo "Built dist/DiscordOverlay"
