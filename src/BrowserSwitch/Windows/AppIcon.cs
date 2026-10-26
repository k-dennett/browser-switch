using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace BrowserSwitch.Windows
{
    internal static class AppIcon
    {
        const string ResourceName = "BrowserSwitch.Icon";
        static Stream Open()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream == null) throw new InvalidOperationException("No se encontró el icono integrado de BrowserSwitch.");
            return stream;
        }
        public static Icon Create()
        {
            using (Stream stream = Open()) return new Icon(stream, 32, 32);
        }
        public static void Save(string path)
        {
            using (Stream stream = Open())
            using (FileStream output = File.Create(path)) stream.CopyTo(output);
        }
    }
}