using System;
using System.Collections.Generic;
using System.IO;

namespace BrowserSwitch.Core
{
    // No dependency on WinForms, the registry or Windows process APIs.
    public sealed class BrowserDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public List<string> Arguments { get; set; }
        public bool IsManual { get; set; }
        public BrowserDefinition() { Arguments = new List<string>(); }
        public override string ToString() { return Name; }
    }

    public sealed class BrowserSettings
    {
        public string PreferredBrowserId { get; set; }
        public List<BrowserDefinition> ManualBrowsers { get; set; }
        public List<string> HiddenBrowserIds { get; set; }
        public BrowserSettings()
        {
            PreferredBrowserId = "";
            ManualBrowsers = new List<BrowserDefinition>();
            HiddenBrowserIds = new List<string>();
        }
        public BrowserDefinition Preferred(IList<BrowserDefinition> browsers)
        {
            foreach (BrowserDefinition browser in browsers)
                if (string.Equals(browser.Id, PreferredBrowserId, StringComparison.OrdinalIgnoreCase)) return browser;
            return null;
        }
    }

    public static class LinkTarget
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new char[] { '\r', '\n', '\0' }) >= 0)
                throw new ArgumentException("Introduce un enlace HTTP/HTTPS completo o un archivo HTML.");
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                !(uri.Scheme == "http" || uri.Scheme == "https" ||
                  (uri.IsFile && (Path.GetExtension(uri.LocalPath).Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                                  Path.GetExtension(uri.LocalPath).Equals(".htm", StringComparison.OrdinalIgnoreCase)))))
                throw new ArgumentException("BrowserSwitch admite enlaces HTTP/HTTPS y archivos HTML.");
            return uri.AbsoluteUri;
        }
    }
}