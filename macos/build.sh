#!/usr/bin/env bash
set -euo pipefail

if [[ $(uname -s) != Darwin ]]; then
    echo "Compila BrowserSwitch.app en macOS con las herramientas de línea de comandos de Xcode." >&2
    exit 1
fi

root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
build="$root/build"
app="$build/BrowserSwitch.app"
mkdir -p "$build"
stage=$(mktemp -d "$build/.browserswitch.XXXXXXXX")
trap 'rm -rf -- "$stage"' EXIT
bundle="$stage/BrowserSwitch.app"
iconset="$stage/BrowserSwitch.iconset"
mkdir -p "$bundle/Contents/"{MacOS,Resources} "$iconset"

cp "$root/Info.plist" "$bundle/Contents/Info.plist"
plutil -lint "$bundle/Contents/Info.plist"
minimum=$(/usr/libexec/PlistBuddy -c 'Print :LSMinimumSystemVersion' "$bundle/Contents/Info.plist")
xcrun --sdk macosx swiftc -swift-version 5 -Osize \
    -target "$(uname -m)-apple-macosx$minimum" -framework AppKit \
    "$root"/Sources/*.swift -o "$bundle/Contents/MacOS/BrowserSwitch"

icon="$root/../docs/assets/browserswitch.png"
cp "$icon" "$bundle/Contents/Resources/BrowserSwitch.png"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$icon" --out "$iconset/icon_${size}x${size}.png" >/dev/null
    pixels=$((size * 2))
    sips -z "$pixels" "$pixels" "$icon" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$bundle/Contents/Resources/BrowserSwitch.icns"
codesign --force --sign - "$bundle"
codesign --verify --strict "$bundle"

if [[ -e "$app" || -L "$app" ]]; then
    mv "$app" "$stage/previous.app"
fi
if ! mv "$bundle" "$app"; then
    if [[ -e "$stage/previous.app" || -L "$stage/previous.app" ]]; then
        mv "$stage/previous.app" "$app"
    fi
    exit 1
fi
du -sh "$app"
