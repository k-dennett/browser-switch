using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

namespace BrowserSwitch.Windows
{
    // Events are scoped to this user/session; tests use an independent identity.
    internal sealed class ResidentSession
    {
        readonly string identity;
        internal ResidentSession(string identity) { this.identity = identity; }
        internal static ResidentSession Current { get { return new ResidentSession(WindowsIdentity.GetCurrent().User.Value); } }
        internal string Name(string action) { return @"Local\BrowserSwitch." + action + "." + identity; }
        delegate bool WindowCallback(IntPtr window, IntPtr state);
        [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr state);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        static bool HasVisibleWindows(int id)
        {
            bool visible = false;
            EnumWindows(delegate(IntPtr window, IntPtr unused) {
                uint owner; GetWindowThreadProcessId(window, out owner);
                if (owner == id && IsWindowVisible(window)) visible = true;
                return true;
            }, IntPtr.Zero);
            return visible;
        }
        internal bool Stop(string executable)
        {
            List<Process> targets = new List<Process>();
            using (Process current = Process.GetCurrentProcess())
            {
                foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
                {
                    bool keep = false;
                    try {
                        keep = process.Id != current.Id && process.SessionId == current.SessionId &&
                            string.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase);
                    }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                    if (keep) targets.Add(process); else process.Dispose();
                }
            }
            try
            {
                if (targets.Count == 0) return false;
                bool coordinated;
                try { using (EventWaitHandle stop = EventWaitHandle.OpenExisting(Name("Stop"))) stop.Set(); coordinated = true; }
                catch (WaitHandleCannotBeOpenedException) { coordinated = false; }
                foreach (Process process in targets)
                {
                    if (process.WaitForExit(coordinated ? 10000 : 1000)) continue;
                    if (coordinated || HasVisibleWindows(process.Id))
                        throw new IOException("Hay una ventana o un editor de BrowserSwitch abierto. Cierra esa ventana y vuelve a pulsar Actualizar; tus cambios no se han descartado.");
                    // Compatibility with v1.0: only an idle, verified local process can be stopped forcibly.
                    process.Kill();
                    if (!process.WaitForExit(10000)) throw new IOException("No se pudo cerrar el residente anterior de BrowserSwitch.");
                }
                return true;
            }
            finally { foreach (Process process in targets) process.Dispose(); }
        }
        internal void Start(string executable, params string[] arguments)
        {
            using (EventWaitHandle ready = new EventWaitHandle(false, EventResetMode.ManualReset, Name("Ready")))
            {
                ready.Reset();
                ProcessStartInfo start = new ProcessStartInfo(executable, CommandLine.Join(arguments)) {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(executable)
                };
                using (Process process = Process.Start(start))
                {
                    using (ManualResetEvent exited = new ManualResetEvent(false))
                    {
                        EventHandler onExit = delegate { try { exited.Set(); } catch (ObjectDisposedException) { } };
                        process.Exited += onExit;
                        try {
                            process.EnableRaisingEvents = true;
                            if (WaitHandle.WaitAny(new WaitHandle[] { ready, exited }, 10000) == 0 && !process.HasExited) return;
                        }
                        finally { process.Exited -= onExit; }
                    }
                    if (!process.HasExited) { process.Kill(); process.WaitForExit(10000); }
                    throw new IOException("La copia instalada no confirmó el inicio de su bandeja.");
                }
            }
        }
    }
}