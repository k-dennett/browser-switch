using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using BrowserSwitch.Core;
using BrowserSwitch.Windows;

namespace BrowserSwitch
{
    internal sealed class BrowserManagerForm : Form
    {
        readonly List<BrowserDefinition> detected;
        readonly ListBox list = new ListBox();
        readonly TextBox details = new TextBox();
        public BrowserSettings Settings { get; private set; }
        public BrowserManagerForm(List<BrowserDefinition> discovered, BrowserSettings original)
        {
            detected = discovered;
            Settings = new BrowserSettings { PreferredBrowserId = original.PreferredBrowserId, HiddenBrowserIds = new List<string>(original.HiddenBrowserIds) };
            foreach (BrowserDefinition browser in original.ManualBrowsers) Settings.ManualBrowsers.Add(Clone(browser));
            Text = "BrowserSwitch — Navegadores";
            ClientSize = new Size(730, 455);
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Controls.Add(new Label { Text = "Navegadores detectados y manuales. Quitar uno detectado lo oculta.", Location = new Point(20, 18), Size = new Size(690, 30) });
            list.Location = new Point(20, 55); list.Size = new Size(690, 190);
            list.SelectedIndexChanged += delegate { ShowDetails(); };
            Controls.Add(list);
            details.Location = new Point(20, 255); details.Size = new Size(690, 62); details.Multiline = true; details.ReadOnly = true; details.ScrollBars = ScrollBars.Horizontal;
            Controls.Add(details);
            Button add = ButtonAt("Agregar…", 20, 335, 120);
            add.Click += delegate { Edit(null); };
            Button edit = ButtonAt("Editar…", 150, 335, 120);
            edit.Click += delegate { if (list.SelectedItem != null) Edit((BrowserDefinition)list.SelectedItem); };
            Button remove = ButtonAt("Quitar", 280, 335, 120);
            remove.Click += delegate
            {
                BrowserDefinition selected = list.SelectedItem as BrowserDefinition;
                if (selected == null) return;
                if (selected.IsManual) Settings.ManualBrowsers.Remove(selected);
                else if (!Settings.HiddenBrowserIds.Contains(selected.Id)) Settings.HiddenBrowserIds.Add(selected.Id);
                if (Settings.PreferredBrowserId == selected.Id) Settings.PreferredBrowserId = "";
                RefreshList();
            };
            Button restore = ButtonAt("Restaurar detectados", 410, 335, 210);
            restore.Click += delegate { Settings.HiddenBrowserIds.Clear(); RefreshList(); };
            Button save = ButtonAt("Guardar", 450, 397, 120); save.DialogResult = DialogResult.OK;
            Button cancel = ButtonAt("Cancelar", 590, 397, 120); cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = save; CancelButton = cancel;
            RefreshList();
        }
        Button ButtonAt(string text, int x, int y, int width)
        {
            Button button = new Button { Text = text, Location = new Point(x, y), Size = new Size(width, 36) };
            Controls.Add(button); return button;
        }
        static BrowserDefinition Clone(BrowserDefinition browser)
        {
            return new BrowserDefinition { Id = browser.Id, Name = browser.Name, ExecutablePath = browser.ExecutablePath, Arguments = new List<string>(browser.Arguments), IsManual = browser.IsManual };
        }
        void Edit(BrowserDefinition selected)
        {
            using (BrowserEditorForm editor = new BrowserEditorForm(selected == null ? null : Clone(selected)))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                BrowserDefinition value = editor.Browser;
                value.IsManual = true;
                if (selected == null || !selected.IsManual) value.Id = "manual:" + Guid.NewGuid().ToString("N");
                if (selected != null)
                {
                    if (selected.IsManual) Settings.ManualBrowsers.Remove(selected);
                    else if (!Settings.HiddenBrowserIds.Contains(selected.Id)) Settings.HiddenBrowserIds.Add(selected.Id);
                    if (Settings.PreferredBrowserId == selected.Id) Settings.PreferredBrowserId = value.Id;
                }
                Settings.ManualBrowsers.Add(value);
                RefreshList();
            }
        }
        void RefreshList()
        {
            list.Items.Clear();
            foreach (BrowserDefinition browser in BrowserDiscovery.Merge(detected, Settings)) list.Items.Add(browser);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            ShowDetails();
        }
        void ShowDetails()
        {
            BrowserDefinition selected = list.SelectedItem as BrowserDefinition;
            details.Text = selected == null ? "" : (selected.IsManual ? "Manual: " : "Detectado: ") + selected.ExecutablePath + Environment.NewLine + "Argumentos fijos: " + CommandLine.Join(selected.Arguments);
        }
    }
    internal sealed class BrowserEditorForm : Form
    {
        readonly TextBox name = new TextBox();
        readonly TextBox executable = new TextBox();
        readonly TextBox arguments = new TextBox();
        public BrowserDefinition Browser { get; private set; }
        public BrowserEditorForm(BrowserDefinition browser)
        {
            Browser = browser ?? new BrowserDefinition();
            Text = browser == null ? "BrowserSwitch — Agregar navegador" : "BrowserSwitch — Editar navegador";
            ClientSize = new Size(680, 305); Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
            AddField("Nombre", name, 20, Browser.Name ?? "");
            AddField("Ejecutable del navegador (.exe)", executable, 82, Browser.ExecutablePath ?? "");
            executable.Width = 520;
            Button browse = new Button { Text = "Examinar…", Location = new Point(550, 105), Size = new Size(110, 30) };
            browse.Click += delegate
            {
                using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Aplicaciones (*.exe)|*.exe", CheckFileExists = true })
                    if (dialog.ShowDialog(this) == DialogResult.OK) executable.Text = dialog.FileName;
            };
            Controls.Add(browse);
            AddField("Argumentos fijos opcionales (ejemplo: --profile-directory=\"Profile 1\")", arguments, 145, CommandLine.Join(Browser.Arguments));
            Controls.Add(new Label { Text = "El enlace se agrega al final. No escribas %1 ni comandos de shell.", Location = new Point(20, 210), Size = new Size(640, 25) });
            Button save = new Button { Text = "Aceptar", Location = new Point(420, 255), Size = new Size(110, 34) };
            save.Click += delegate
            {
                try
                {
                    BrowserDefinition value = new BrowserDefinition { Id = Browser.Id, Name = name.Text.Trim(), ExecutablePath = Environment.ExpandEnvironmentVariables(executable.Text.Trim().Trim('"')), Arguments = CommandLine.Split(arguments.Text), IsManual = true };
                    foreach (string arg in value.Arguments)
                        if (arg.IndexOf("%1", StringComparison.OrdinalIgnoreCase) >= 0 || arg.IndexOf("%l", StringComparison.OrdinalIgnoreCase) >= 0 || arg.Contains("%*")) throw new ArgumentException("No incluyas marcadores de enlace. BrowserSwitch agrega la dirección por separado.");
                    BrowserLauncher.Validate(value);
                    value.ExecutablePath = Path.GetFullPath(value.ExecutablePath);
                    Browser = value; DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "BrowserSwitch", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            Controls.Add(save);
            Button cancel = new Button { Text = "Cancelar", Location = new Point(550, 255), Size = new Size(110, 34), DialogResult = DialogResult.Cancel };
            Controls.Add(cancel); AcceptButton = save; CancelButton = cancel;
        }
        void AddField(string label, TextBox box, int y, string value)
        {
            Controls.Add(new Label { Text = label, Location = new Point(20, y), Size = new Size(640, 23) });
            box.Location = new Point(20, y + 25); box.Size = new Size(640, 26); box.Text = value; box.AccessibleName = label;
            Controls.Add(box);
        }
    }
}