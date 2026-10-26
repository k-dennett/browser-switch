using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using BrowserSwitch.Core;
using BrowserSwitch.Windows;

namespace BrowserSwitch
{
    // Migrated from SelectorNavegador/SelectorNavegador.cs: same choose/remember/ask workflow.
    internal sealed class SelectorForm : Form
    {
        readonly string url;
        readonly SettingsStore store;
        BrowserSettings settings;
        List<BrowserDefinition> detected;
        List<BrowserDefinition> browsers;
        readonly List<string> warnings;
        readonly CheckBox remember = new CheckBox();
        readonly Label status = new Label();
        readonly TextBox linkInput = new TextBox();
        readonly FlowLayoutPanel choices = new FlowLayoutPanel();

        public SelectorForm(string link, SettingsStore settingsStore, BrowserSettings configuration, List<BrowserDefinition> discovered, List<string> discoveryWarnings)
        {
            url = link;
            store = settingsStore;
            settings = configuration;
            detected = discovered;
            warnings = discoveryWarnings;
            Text = "BrowserSwitch";
            ClientSize = new Size(660, 590);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 10);
            BackColor = Color.FromArgb(246, 248, 252);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(new Label { Text = link == null ? "¿Dónde abrir tus próximos enlaces?" : "¿Con qué navegador abrir este enlace?", Location = new Point(24, 20), Size = new Size(612, 35), Font = new Font("Segoe UI", 15, FontStyle.Bold) });
            Controls.Add(new Label { Text = link == null ? "Pega un enlace, o deja vacío para guardar un navegador fijo:" : link, Location = new Point(24, 62), Size = new Size(612, 48), AutoEllipsis = true });
            linkInput.Location = new Point(24, 91);
            linkInput.Size = new Size(612, 26);
            linkInput.Visible = link == null;
            linkInput.AccessibleName = "Enlace HTTP o HTTPS";
            Controls.Add(linkInput);
            linkInput.BringToFront();
            choices.Location = new Point(24, 130);
            choices.Size = new Size(612, 230);
            choices.AutoScroll = true;
            choices.FlowDirection = FlowDirection.TopDown;
            choices.WrapContents = false;
            Controls.Add(choices);
            remember.Text = "Usar esta elección para los próximos enlaces";
            remember.Location = new Point(24, 368);
            remember.Size = new Size(612, 30);
            remember.Visible = link != null;
            Controls.Add(remember);
            Button ask = new Button { Text = "Preguntar siempre", Location = new Point(24, 412), Size = new Size(182, 38) };
            ask.Click += delegate { Try(delegate { settings.PreferredBrowserId = ""; store.Save(settings); UpdateStatus(); }); };
            Controls.Add(ask);
            Button defaults = new Button { Text = UserInstallation.IsStable ? "Configurar en Windows" : "Instalar BrowserSwitch", Location = new Point(216, 412), Size = new Size(218, 38) };
            defaults.Click += delegate { Try(delegate { if (UserInstallation.IsStable) WindowsRegistration.Register(); else UserInstallation.Install(); WindowsRegistration.OpenDefaults(); }); };
            Controls.Add(defaults);
            Button manage = new Button { Text = "Navegadores…", Location = new Point(444, 412), Size = new Size(192, 38) };
            manage.Click += delegate
            {
                Try(delegate
                {
                    BrowserDiscovery discovery = new BrowserDiscovery();
                    detected = discovery.Detect();
                    using (BrowserManagerForm manager = new BrowserManagerForm(detected, settings))
                        if (manager.ShowDialog(this) == DialogResult.OK) { store.Save(manager.Settings); settings = manager.Settings; }
                    RefreshChoices();
                });
            };
            Controls.Add(manage);
            status.Location = new Point(24, 474);
            status.Size = new Size(612, 96);
            Controls.Add(status);
            RefreshChoices();
            Activated += delegate { Try(delegate { settings = store.Load(); RefreshChoices(); }); };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) Close();
                int index = !linkInput.ContainsFocus && e.Modifiers == Keys.None && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9 ? e.KeyCode - Keys.D1 : -1;
                if (index >= 0 && index < browsers.Count) { e.SuppressKeyPress = true; Try(delegate { Choose(browsers[index]); }); }
            };
        }
        void Try(Action action)
        {
            try { action(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "BrowserSwitch", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        void RefreshChoices()
        {
            browsers = BrowserDiscovery.Merge(detected, settings);
            while (choices.Controls.Count > 0) choices.Controls[0].Dispose();
            for (int i = 0; i < browsers.Count; i++)
            {
                BrowserDefinition browser = browsers[i];
                Button button = new Button { Text = (i < 9 ? "&" + (i + 1) + "  " : "") + browser.Name, Size = new Size(580, 48), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Enabled = File.Exists(browser.ExecutablePath), AccessibleDescription = browser.ExecutablePath };
                button.Click += delegate { Try(delegate { Choose(browser); }); };
                choices.Controls.Add(button);
            }
            if (browsers.Count == 0) choices.Controls.Add(new Label { Text = "No se detectaron navegadores. Agrega uno desde Navegadores…", Size = new Size(570, 70) });
            UpdateStatus();
        }
        void Choose(BrowserDefinition browser)
        {
            string target = url ?? linkInput.Text.Trim();
            if (target.Length == 0)
            {
                BrowserLauncher.Validate(browser);
                settings.PreferredBrowserId = browser.Id;
                store.Save(settings);
                UpdateStatus();
                return;
            }
            BrowserLauncher.Open(browser, LinkTarget.Normalize(target));
            if (remember.Checked) { settings.PreferredBrowserId = browser.Id; store.Save(settings); }
            Close();
        }
        void UpdateStatus()
        {
            if (browsers == null) return;
            BrowserDefinition selected = settings.Preferred(browsers);
            status.Text = "Modo guardado: " + (selected == null ? "preguntar en cada enlace" : selected.Name) + ".\n" +
                "Windows: HTTP " + (WindowsRegistration.IsDefault("http") ? "activo" : "pendiente") + ", HTTPS " + (WindowsRegistration.IsDefault("https") ? "activo" : "pendiente") + ".\n" +
                (warnings.Count > 0 ? "Algunas entradas del registro no se pudieron leer. Puedes agregar un navegador manualmente." : "Teams debe usar Navegador predeterminado para respetar esta elección.");
        }
    }
}