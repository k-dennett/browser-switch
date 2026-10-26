using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BrowserSwitch.Core;

namespace BrowserSwitch.Windows
{
    public static class BrowserLauncher
    {
        public static bool IsSelf(string path)
        {
            string name = Path.GetFileName(path);
            return name.Equals("BrowserSwitch.exe", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("SelectorNavegador.exe", StringComparison.OrdinalIgnoreCase) ||
                   Path.GetFullPath(path).Equals(System.Reflection.Assembly.GetExecutingAssembly().Location, StringComparison.OrdinalIgnoreCase);
        }
        public static void Validate(BrowserDefinition browser)
        {
            if (browser == null || string.IsNullOrWhiteSpace(browser.Name)) throw new ArgumentException("Escribe el nombre del navegador.");
            if (string.IsNullOrWhiteSpace(browser.ExecutablePath) || !Path.IsPathRooted(browser.ExecutablePath) ||
                !File.Exists(browser.ExecutablePath) || !Path.GetExtension(browser.ExecutablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Selecciona la ruta completa de un navegador .exe existente.");
            if (IsSelf(browser.ExecutablePath)) throw new ArgumentException("No puedes agregar BrowserSwitch como su propio navegador.");
            string name = Path.GetFileNameWithoutExtension(browser.ExecutablePath).ToLowerInvariant();
            if (name == "cmd" || name == "powershell" || name == "pwsh" || name == "wscript" || name == "cscript" || name == "mshta" || name == "rundll32")
                throw new ArgumentException("Selecciona el ejecutable del navegador, no un intérprete o un lanzador del sistema.");
            foreach (string argument in browser.Arguments)
                if (argument == null || argument.IndexOfAny(new char[] { '\r', '\n', '\0' }) >= 0)
                    throw new ArgumentException("Los argumentos no pueden contener saltos de línea ni caracteres nulos.");
        }
        public static ProcessStartInfo CreateStartInfo(BrowserDefinition browser, string link)
        {
            Validate(browser);
            List<string> args = new List<string>(browser.Arguments);
            // The URL is always one separate final argument; it never becomes shell source.
            args.Add(LinkTarget.Normalize(link));
            return new ProcessStartInfo(browser.ExecutablePath, CommandLine.Join(args))
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(browser.ExecutablePath)
            };
        }
        public static void Open(BrowserDefinition browser, string link)
        {
            using (Process process = Process.Start(CreateStartInfo(browser, link))) { }
        }
    }
}