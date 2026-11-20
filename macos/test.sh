#!/usr/bin/env bash
set -euo pipefail

root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
if ! command -v swiftc >/dev/null; then
    echo "Se requiere swiftc para ejecutar las pruebas." >&2
    exit 1
fi

scratch=$(mktemp -d "${TMPDIR:-/tmp}/browserswitch-tests.XXXXXXXX")
trap 'rm -rf -- "$scratch"' EXIT
swiftc -swift-version 5 "$root/Sources/Routing.swift" "$root/Tests/RoutingTests.swift" \
    -o "$scratch/RoutingTests"
"$scratch/RoutingTests"
