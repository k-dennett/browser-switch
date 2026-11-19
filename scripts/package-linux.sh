#!/usr/bin/env bash
set -euo pipefail

repo=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
output="$repo/artifacts"
name=BrowserSwitch-linux
stage=$(mktemp -d)
trap 'rm -rf -- "$stage"' EXIT

dotnet publish "$repo/src/BrowserSwitch.Linux/BrowserSwitch.Linux.csproj" \
    --configuration Release --no-self-contained -p:DebugType=None --output "$stage/$name"
cp -- "$repo/packaging/linux/install.py" "$stage/$name/"
mkdir -p -- "$output"
tar -czf "$stage/$name.tar.gz" -C "$stage" "$name"
mv -- "$stage/$name.tar.gz" "$output/$name.tar.gz"
printf 'Paquete: %s (%s bytes)\n' "$output/$name.tar.gz" "$(stat -c %s "$output/$name.tar.gz")"
