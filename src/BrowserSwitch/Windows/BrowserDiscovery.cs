using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using BrowserSwitch.Core;
using Microsoft.Win32;

namespace BrowserSwitch.Windows
{
    public sealed class BrowserDiscovery
    {
        public readonly List<string> Warnings = new List<string>();
        public List<BrowserDefinition> Detect()
        {
            List<BrowserDefinition> found = new List<BrowserDefinition>();
            foreach (RegistryHive hive in new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (RegistryView view in Environment.Is64BitOperatingSystem ? new RegistryView[] { RegistryView.Registry64, RegistryView.Registry32 } : new RegistryView[] { RegistryView.Registry32 })
                {
                    try
                    {
                        using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                        {
                            using (RegistryKey clients = root.OpenSubKey(@"Software\Clients\StartMenuInternet"))
                                if (clients != null) foreach (string clientName in clients.GetSubKeyNames())
                                {
                                    if (clientName.Equals(WindowsRegistration.AppId, StringComparison.OrdinalIgnoreCase)) continue;
                                    using (RegistryKey client = clients.OpenSubKey(clientName))
                                    {
                                        if (client == null) continue;
                                        using (RegistryKey command = client.OpenSubKey(@"shell\open\command"))
                                            Add(found, Convert.ToString(client.GetValue("", clientName)), command == null ? null : command.GetValue("") as string);
                                    }
                                }
                            using (RegistryKey registered = root.OpenSubKey(@"Software\RegisteredApplications"))
                                if (registered != null) foreach (string name in registered.GetValueNames())
                                {
                                    string path = registered.GetValue(name) as string;
                                    if (string.IsNullOrEmpty(path) || path.IndexOf(WindowsRegistration.AppId, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                                    using (RegistryKey caps = root.OpenSubKey(path))
                                    using (RegistryKey urls = caps == null ? null : caps.OpenSubKey("URLAssociations"))
                                    {
                                        if (urls == null) continue;
                                        string id = (urls.GetValue("https") ?? urls.GetValue("http")) as string;
                                        if (string.IsNullOrEmpty(id) || id.Equals(WindowsRegistration.AppId, StringComparison.OrdinalIgnoreCase)) continue;
                                        // HKCU classes override machine classes, as in the Windows shell.
                                        string command = ReadCommand(RegistryHive.CurrentUser, view, id) ?? ReadCommand(RegistryHive.LocalMachine, view, id);
                                        Add(found, Convert.ToString(caps.GetValue("ApplicationName", name)), command);
                                    }
                                }
                        }
                    }
                    catch (UnauthorizedAccessException ex) { Warnings.Add(hive + "/" + view + ": " + ex.Message); }
                    catch (SecurityException ex) { Warnings.Add(hive + "/" + view + ": " + ex.Message); }
                    catch (IOException ex) { Warnings.Add(hive + "/" + view + ": " + ex.Message); }
                }
            // Preserve the original known-path fallback for browsers missing registration.
            AddKnown(found, "Chrome", Environment.SpecialFolder.ProgramFiles, @"Google\Chrome\Application\chrome.exe");
            AddKnown(found, "Chrome", Environment.SpecialFolder.LocalApplicationData, @"Google\Chrome\Application\chrome.exe");
            AddKnown(found, "Comet", Environment.SpecialFolder.LocalApplicationData, @"Perplexity\Comet\Application\comet.exe");
            AddKnown(found, "Edge", Environment.SpecialFolder.ProgramFilesX86, @"Microsoft\Edge\Application\msedge.exe");
            AddKnown(found, "Edge", Environment.SpecialFolder.ProgramFiles, @"Microsoft\Edge\Application\msedge.exe");
            return found;
        }
        static string ReadCommand(RegistryHive hive, RegistryView view, string id)
        {
            using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
            using (RegistryKey command = root.OpenSubKey(@"Software\Classes\" + id + @"\shell\open\command"))
                return command == null ? null : command.GetValue("") as string;
        }
        static void AddKnown(List<BrowserDefinition> result, string name, Environment.SpecialFolder folder, string relative)
        {
            string root = Environment.GetFolderPath(folder);
            if (root.Length > 0) Add(result, name, CommandLine.Quote(Path.Combine(root, relative)));
        }
        public static BrowserDefinition FromCommand(string name, string command)
        {
            try { return ParseCommand(name, command); }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
        }
        static BrowserDefinition ParseCommand(string name, string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            List<string> tokens = CommandLine.Split(Environment.ExpandEnvironmentVariables(command));
            if (tokens.Count == 0) return null;
            string exe = tokens[0];
            if (!Path.IsPathRooted(exe) || !File.Exists(exe) || BrowserLauncher.IsSelf(exe)) return null;
            List<string> arguments = new List<string>();
            for (int i = 1; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (token.Equals("%1", StringComparison.OrdinalIgnoreCase) || token.Equals("%l", StringComparison.OrdinalIgnoreCase) || token == "%*") continue;
                // Unsupported embedded shell placeholders require a manual entry.
                if (token.IndexOf("%1", StringComparison.OrdinalIgnoreCase) >= 0 || token.IndexOf("%l", StringComparison.OrdinalIgnoreCase) >= 0 || token.Contains("%*")) return null;
                arguments.Add(token);
            }
            BrowserDefinition browser = new BrowserDefinition { Name = name, ExecutablePath = Path.GetFullPath(exe), Arguments = arguments };
            browser.Id = "detected:" + Fingerprint(browser);
            try { BrowserLauncher.Validate(browser); }
            catch (ArgumentException) { return null; }
            return browser;
        }
        public static string Fingerprint(BrowserDefinition browser)
        {
            // Same executable and fixed arguments represent the same launch target.
            string value = Path.GetFullPath(browser.ExecutablePath).ToUpperInvariant() + "\n" + CommandLine.Join(browser.Arguments);
            using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        static void Add(List<BrowserDefinition> found, string name, string command)
        {
            BrowserDefinition browser = FromCommand(name, command);
            if (browser == null) return;
            foreach (BrowserDefinition other in found) if (string.Equals(other.ExecutablePath, browser.ExecutablePath, StringComparison.OrdinalIgnoreCase)) return;
            found.Add(browser);
        }
        public static List<BrowserDefinition> Merge(List<BrowserDefinition> detected, BrowserSettings settings)
        {
            List<BrowserDefinition> result = new List<BrowserDefinition>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Manual edits take precedence over detected entries with the same command.
            foreach (BrowserDefinition browser in settings.ManualBrowsers)
                if (!settings.HiddenBrowserIds.Contains(browser.Id) && seen.Add(Fingerprint(browser))) result.Add(browser);
            foreach (BrowserDefinition browser in detected)
                if (!settings.HiddenBrowserIds.Contains(browser.Id) && seen.Add(Fingerprint(browser))) result.Add(browser);
            result.Sort(delegate(BrowserDefinition a, BrowserDefinition b) { return StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name); });
            return result;
        }
    }
}