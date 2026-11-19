#!/usr/bin/env python3
import argparse
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import tempfile

DESKTOP_ID = "io.github.k_dennett.BrowserSwitch.desktop"
APPLICATION = "BrowserSwitch.Linux.dll"
FILES = (
    APPLICATION,
    "BrowserSwitch.Linux.deps.json",
    "BrowserSwitch.Linux.runtimeconfig.json",
    "browserswitch.png",
    "install.py",
)
ASSOCIATION_GROUPS = {"Default Applications", "Added Associations", "Removed Associations"}


def xdg_home(variable, default):
    value = Path(os.environ.get(variable, ""))
    return value if value.is_absolute() else Path.home() / default


def find_dotnet():
    for candidate in (shutil.which("dotnet"), Path.home() / ".dotnet" / "dotnet"):
        if not candidate or not os.access(candidate, os.X_OK) or not Path(candidate).is_file():
            continue
        try:
            runtimes = subprocess.check_output([candidate, "--list-runtimes"],
                                               stderr=subprocess.DEVNULL, text=True, timeout=5)
        except (OSError, subprocess.SubprocessError):
            continue
        if any(line.startswith("Microsoft.NETCore.App 10.") for line in runtimes.splitlines()):
            return Path(candidate).absolute()
    return None


def desktop_string(value):
    return (str(value).replace("\\", "\\\\").replace("\n", "\\n")
            .replace("\r", "\\r").replace("\t", "\\t"))


def exec_argument(path):
    # Exec quoting is applied before the desktop-entry string escaping.
    value = str(path).replace("%", "%%")
    value = re.sub(r'(["\\$\x60])', r'\\\1', value)
    return desktop_string(f'"{value}"')


def write_file(path, content):
    path = path.resolve()
    mode = stat.S_IMODE(path.stat().st_mode) if path.exists() else 0o644
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as stream:
        temporary = Path(stream.name)
        try:
            os.fchmod(stream.fileno(), mode)
            stream.write(content)
            stream.close()
            temporary.replace(path)
        finally:
            temporary.unlink(missing_ok=True)


def install(source, payload, desktop, autostart):
    dotnet = find_dotnet()
    if not dotnet:
        raise ValueError("Instala el runtime de .NET 10 y deja dotnet en PATH o ~/.dotnet.")
    refresh = shutil.which("update-desktop-database")
    if not refresh:
        raise ValueError("Se necesita desktop-file-utils para registrar HTTP y HTTPS.")
    if not all((source / name).is_file() for name in FILES):
        raise ValueError("Extrae el paquete completo antes de ejecutar install.py.")

    for name in FILES:
        write_file(payload / name, (source / name).read_bytes())

    command = f"{exec_argument(dotnet)} {exec_argument(payload / APPLICATION)}"
    common = (
        "[Desktop Entry]\n"
        "Type=Application\n"
        "Name=BrowserSwitch\n"
        f"Icon={desktop_string(payload / 'browserswitch.png')}\n"
        f"TryExec={desktop_string(dotnet)}\n"
        "Terminal=false\n"
    )
    write_file(desktop, (
        common + f"Exec={command} %u\n"
        "Comment=Elegir qué navegador abrirá los enlaces\n"
        "Categories=Network;WebBrowser;\n"
        "MimeType=x-scheme-handler/http;x-scheme-handler/https;\n"
    ).encode())
    subprocess.run([refresh, desktop.parent], check=True)
    write_file(autostart, (common + f"Exec={command} --tray\n").encode())
    print("BrowserSwitch instalado para tu usuario. Ábrelo desde el menú de aplicaciones.")
    print("Puedes elegirlo como navegador predeterminado en Configuración de GNOME.")


def remove_associations(path):
    if not path.is_file():
        return
    original = path.read_bytes().decode("utf-8")
    remaining = []
    group = ""
    for line in original.splitlines(keepends=True):
        stripped = line.strip()
        if stripped.startswith("[") and stripped.endswith("]"):
            group = stripped[1:-1]
        if group in ASSOCIATION_GROUPS and not stripped.startswith(("#", ";")) and "=" in line:
            key, value = line.rstrip("\r\n").split("=", 1)
            entries = value.split(";")
            if any(entry.strip() == DESKTOP_ID for entry in entries):
                entries = [entry for entry in entries if entry.strip() != DESKTOP_ID]
                if any(entry.strip() for entry in entries):
                    ending = line[len(line.rstrip("\r\n")):]
                    remaining.append(key + "=" + ";".join(entries) + ending)
                continue
        remaining.append(line)
    updated = "".join(remaining)
    if updated != original:
        write_file(path, updated.encode())


def uninstall(config, payload, desktop, autostart):
    # Clear defaults and association overrides before removing their launcher.
    for directory in (config, desktop.parent):
        for path in (directory / "mimeapps.list", directory / "defaults.list",
                     *directory.glob("*-mimeapps.list")):
            remove_associations(path)
    autostart.unlink(missing_ok=True)
    desktop.unlink(missing_ok=True)
    refresh = shutil.which("update-desktop-database")
    if refresh and desktop.parent.is_dir():
        if subprocess.run([refresh, desktop.parent], check=False).returncode:
            print("No se pudo actualizar la caché de aplicaciones.")

    dotnet = find_dotnet()
    if dotnet and (payload / APPLICATION).is_file():
        try:
            subprocess.run([dotnet, payload / APPLICATION, "--quit"],
                           check=True, timeout=5, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        except (OSError, subprocess.SubprocessError):
            print("Si BrowserSwitch sigue abierto, ciérralo desde la bandeja o al cerrar sesión.")

    for name in FILES:
        (payload / name).unlink(missing_ok=True)
    if payload.is_dir() and not any(payload.iterdir()):
        payload.rmdir()
    print("BrowserSwitch desinstalado. Se conservó tu preferencia.")


def main():
    parser = argparse.ArgumentParser(description="Instalación por usuario de BrowserSwitch para Linux")
    parser.add_argument("action", choices=("install", "uninstall"))
    args = parser.parse_args()
    data = xdg_home("XDG_DATA_HOME", ".local/share")
    config = xdg_home("XDG_CONFIG_HOME", ".config")
    payload = data / "browser-switch"
    desktop = data / "applications" / DESKTOP_ID
    autostart = config / "autostart" / DESKTOP_ID
    try:
        if args.action == "install":
            install(Path(__file__).resolve().parent, payload, desktop, autostart)
        else:
            uninstall(config, payload, desktop, autostart)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        parser.exit(1, f"BrowserSwitch: {error}\n")


if __name__ == "__main__":
    main()
