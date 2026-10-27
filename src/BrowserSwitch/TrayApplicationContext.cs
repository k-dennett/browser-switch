using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using BrowserSwitch.Core;
using BrowserSwitch.Windows;

namespace BrowserSwitch
{
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        readonly SettingsStore store = new SettingsStore();
        readonly NotifyIcon notification;
        readonly ContextMenuStrip menu;
        readonly Icon icon;
        readonly Control dispatcher;
        readonly RegisteredWaitHandle activationWait;
        readonly RegisteredWaitHandle windowWait;
        readonly RegisteredWaitHandle shutdownWait;
        SelectorForm selector;
        volatile bool disposing;

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr handle);

        public static void Run(bool showMenu, bool showWindow) { Run(showMenu, showWindow, ResidentSession.Current); }
        internal static void Run(bool showMenu, bool showWindow, ResidentSession session)
        {
            using (EventWaitHandle activation = new EventWaitHandle(false, EventResetMode.AutoReset, session.Name("ShowMenu")))
            using (EventWaitHandle windowActivation = new EventWaitHandle(false, EventResetMode.AutoReset, session.Name("ShowWindow")))
            using (EventWaitHandle shutdown = new EventWaitHandle(false, EventResetMode.AutoReset, session.Name("Stop")))
            using (EventWaitHandle ready = new EventWaitHandle(false, EventResetMode.ManualReset, session.Name("Ready")))
            {
                bool first;
                using (Mutex instance = new Mutex(false, session.Name("Tray"), out first))
                {
                    if (!first) { if (showWindow) windowActivation.Set(); else if (showMenu) activation.Set(); return; }
                    using (TrayApplicationContext context = new TrayApplicationContext(activation, windowActivation, shutdown, showMenu, showWindow))
                    {
                        ready.Set();
                        try { Application.Run(context); }
                        finally { ready.Reset(); }
                    }
                }
            }
        }
        TrayApplicationContext(EventWaitHandle activation, EventWaitHandle windowActivation, EventWaitHandle shutdown, bool showMenu, bool showWindow)
        {
            dispatcher = new Control();
            // A message-only dispatcher: no visible window, timer or polling loop.
            IntPtr handle = dispatcher.Handle;
            menu = new TrayContextMenu();
            menu.Font = new Font("Segoe UI", 10);
            menu.Opening += delegate { RebuildMenu(); };
            icon = AppIcon.Create();
            notification = new NotifyIcon { Icon = icon, ContextMenuStrip = menu, Text = "BrowserSwitch", Visible = true };
            notification.MouseUp += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowMenu(); };
            activationWait = Watch(activation, ShowMenu);
            windowWait = Watch(windowActivation, delegate { Try(ShowSelector); });
            shutdownWait = Watch(shutdown, delegate {
                // Do not discard a browser editor's uncommitted changes.
                foreach (Form form in Application.OpenForms) if (form.Modal) return;
                ExitThread();
            });
            UpdateTooltip();
            if (showWindow) dispatcher.BeginInvoke(new Action(delegate { Try(ShowSelector); }));
            else if (showMenu) dispatcher.BeginInvoke(new Action(ShowMenu));
        }
        RegisteredWaitHandle Watch(EventWaitHandle signal, Action action)
        {
            return ThreadPool.RegisterWaitForSingleObject(signal, delegate {
                if (disposing) return;
                try { dispatcher.BeginInvoke(action); }
                catch (InvalidOperationException) { }
            }, null, Timeout.Infinite, false);
        }
        void Try(Action action)
        {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "BrowserSwitch", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void ShowMenu()
        {
            if (disposing) return;
            if (selector != null && !selector.IsDisposed && selector.Visible) menu.Show(selector, selector.PointToClient(Cursor.Position));
            else menu.Show(Cursor.Position);
            SetForegroundWindow(menu.Handle);
        }
        void RebuildMenu()
        {
            while (menu.Items.Count > 0) menu.Items[0].Dispose();
            try
            {
                BrowserSettings settings = store.Load();
                BrowserDiscovery discovery = new BrowserDiscovery();
                List<BrowserDefinition> browsers = BrowserDiscovery.Merge(discovery.Detect(), settings);
                BrowserDefinition selected = settings.Preferred(browsers);
                string current = selected == null ? "Preguntar siempre" : selected.Name;
                SetTooltip(current);
                menu.Items.Add(new ToolStripMenuItem("BrowserSwitch · " + current) { Enabled = false });
                menu.Items.Add(new ToolStripSeparator());
                foreach (BrowserDefinition browser in browsers)
                {
                    BrowserDefinition choice = browser;
                    ToolStripMenuItem item = new ToolStripMenuItem(browser.Name) { Checked = selected != null && selected.Id == browser.Id, Enabled = File.Exists(browser.ExecutablePath), ToolTipText = browser.ExecutablePath };
                    item.Click += delegate { Try(delegate { BrowserLauncher.Validate(choice); BrowserSettings latest = store.Load(); latest.PreferredBrowserId = choice.Id; store.Save(latest); SetTooltip(choice.Name); }); };
                    menu.Items.Add(item);
                }
                if (browsers.Count == 0) menu.Items.Add(new ToolStripMenuItem("No hay navegadores disponibles") { Enabled = false });
                ToolStripMenuItem ask = new ToolStripMenuItem("Preguntar siempre") { Checked = selected == null };
                ask.Click += delegate { Try(delegate { BrowserSettings latest = store.Load(); latest.PreferredBrowserId = ""; store.Save(latest); SetTooltip("Preguntar siempre"); }); };
                menu.Items.Add(ask);
                menu.Items.Add(new ToolStripSeparator());
                if (store.Warning != null) menu.Items.Add(new ToolStripMenuItem("Configuración recuperada; ver ventana…", null, delegate { ShowSelector(); }));
            }
            catch (Exception ex) { menu.Items.Add(new ToolStripMenuItem("No se pudo leer la configuración") { Enabled = false, ToolTipText = ex.Message }); }
            menu.Items.Add("Abrir ventana…", null, delegate { Try(ShowSelector); });
            menu.Items.Add("Navegadores…", null, delegate { Try(ManageBrowsers); });
            ToolStripMenuItem startup = new ToolStripMenuItem("Iniciar con Windows") { Checked = StartupRegistration.IsEnabled };
            startup.Click += delegate { Try(delegate { StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled); }); };
            menu.Items.Add(startup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir de la bandeja", null, delegate { ExitThread(); });
        }
        void SetTooltip(string current)
        {
            string text = "BrowserSwitch — " + current;
            notification.Text = text.Length > 63 ? text.Substring(0, 60) + "…" : text;
        }
        void UpdateTooltip()
        {
            Try(delegate
            {
                BrowserSettings settings = store.Load();
                BrowserDefinition selected = settings.Preferred(BrowserDiscovery.Merge(new BrowserDiscovery().Detect(), settings));
                SetTooltip(selected == null ? "Preguntar siempre" : selected.Name);
            });
        }
        void ShowSelector()
        {
            if (selector != null && !selector.IsDisposed)
            {
                if (selector.WindowState == FormWindowState.Minimized) selector.WindowState = FormWindowState.Normal;
                selector.Show(); selector.Activate(); return;
            }
            BrowserDiscovery discovery = new BrowserDiscovery();
            List<BrowserDefinition> detected = discovery.Detect();
            selector = new SelectorForm(null, store, store.Load(), detected, discovery.Warnings);
            selector.Icon = icon;
            selector.FormClosed += delegate { selector = null; UpdateTooltip(); };
            selector.Show(); selector.Activate();
        }
        void ManageBrowsers()
        {
            using (BrowserManagerForm manager = new BrowserManagerForm(new BrowserDiscovery().Detect(), store.Load()))
                if (manager.ShowDialog() == DialogResult.OK) store.Save(manager.Settings);
            UpdateTooltip();
        }
        protected override void ExitThreadCore()
        {
            if (selector != null && !selector.IsDisposed) selector.Close();
            notification.Visible = false;
            base.ExitThreadCore();
        }
        protected override void Dispose(bool disposingManaged)
        {
            if (disposingManaged && !disposing)
            {
                disposing = true;
                activationWait.Unregister(null);
                windowWait.Unregister(null);
                shutdownWait.Unregister(null);
                notification.Visible = false;
                notification.Dispose(); menu.Dispose(); icon.Dispose(); dispatcher.Dispose();
            }
            base.Dispose(disposingManaged);
        }
    }
}
