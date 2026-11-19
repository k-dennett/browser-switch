#nullable enable
using System.Runtime.InteropServices;
using BrowserSwitch.Core;
using static BrowserSwitch.Linux.Native;

namespace BrowserSwitch.Linux;

internal sealed partial class Program
{
    private readonly Dictionary<string, nint> trayChoices = new();
    private nint indicator;
    private string trayPreference = "";
    private bool refreshingTray;

    private void StartTray()
    {
        if (indicator != 0 || !NativeLibrary.TryLoad(Ayatana, out var library)) return;
        try
        {
            var settings = preferences.Load();
            EnsureGtk();
            var menu = gtk_menu_new();

            AddTrayChoice(menu, "", Ask);
            foreach (var (id, name, _) in DesktopBrowsers.Supported)
                AddTrayChoice(menu, id, name);

            gtk_menu_shell_append(menu, gtk_separator_menu_item_new());
            AddTrayAction(menu, "Preferencias…", ShowSettings);
            AddTrayAction(menu, "Salir", () => g_application_quit(application));
            gtk_widget_show_all(menu);
            RefreshTray(settings.PreferredBrowserId);

            indicator = app_indicator_new("browser-switch", iconPath, 0); // APPLICATION_STATUS
            app_indicator_set_title(indicator, "BrowserSwitch");
            app_indicator_set_menu(indicator, menu);
            app_indicator_set_status(indicator, 1); // ACTIVE
            g_application_hold(application);
        }
        finally { NativeLibrary.Free(library); }
    }

    private void AddTrayChoice(nint menu, string id, string caption)
    {
        var item = gtk_check_menu_item_new_with_label(caption);
        gtk_check_menu_item_set_draw_as_radio(item, 1);
        gtk_menu_shell_append(menu, item);
        trayChoices.Add(id, item);
        Connect(item, "toggled", (WidgetSignal)((_, _) =>
        {
            if (refreshingTray) return;
            if (id.Length != 0 && !DesktopBrowsers.Detect().Exists(browser => browser.Id == id))
            {
                RefreshTray(trayPreference);
                return;
            }
            if (!SavePreference(id)) RefreshTray(trayPreference);
        }));
    }

    private void RefreshTray(string id, List<BrowserDefinition>? available = null)
    {
        if (trayChoices.Count == 0) return;
        trayPreference = id;
        available ??= DesktopBrowsers.Detect();
        var active = new BrowserSettings { PreferredBrowserId = id }.Preferred(available)?.Id ?? "";
        refreshingTray = true;
        try
        {
            foreach (var (key, item) in trayChoices)
            {
                gtk_widget_set_sensitive(item, key.Length == 0 || available.Exists(browser => browser.Id == key) ? 1 : 0);
                gtk_check_menu_item_set_active(item, key == active ? 1 : 0);
            }
        }
        finally { refreshingTray = false; }
    }

    private void AddTrayAction(nint menu, string caption, Action activate)
    {
        var item = gtk_menu_item_new_with_label(caption);
        gtk_menu_shell_append(menu, item);
        Connect(item, "activate", (WidgetSignal)((_, _) =>
        {
            try { activate(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowError($"No se pudo leer la preferencia. {ex.Message}");
            }
        }));
    }
}
