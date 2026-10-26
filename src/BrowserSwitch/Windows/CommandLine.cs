using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BrowserSwitch.Windows
{
    public static class CommandLine
    {
        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CommandLineToArgvW(string commandLine, out int argc);
        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr memory);

        public static List<string> Split(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return new List<string>();
            int count;
            // A dummy argv[0] gives every actual token the same parsing rules.
            IntPtr memory = CommandLineToArgvW("BrowserSwitch.exe " + commandLine, out count);
            if (memory == IntPtr.Zero) throw new ArgumentException("No se pudieron interpretar los argumentos.");
            try
            {
                List<string> result = new List<string>();
                for (int i = 1; i < count; i++) result.Add(Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory, i * IntPtr.Size)));
                return result;
            }
            finally { LocalFree(memory); }
        }

        // Inverse of Windows argv quoting, including quotes and trailing backslashes.
        public static string Quote(string value)
        {
            StringBuilder result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }
        public static string Join(IEnumerable<string> arguments)
        {
            List<string> quoted = new List<string>();
            foreach (string argument in arguments) quoted.Add(Quote(argument));
            return string.Join(" ", quoted.ToArray());
        }
    }
}