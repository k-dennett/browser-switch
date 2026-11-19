#nullable enable
using System.ComponentModel;
using System.Runtime.InteropServices;
using BrowserSwitch.Core;
using static BrowserSwitch.Linux.Native;

namespace BrowserSwitch.Linux;

internal sealed partial class Program
{
    private const string ApplicationId = "io.github.k_dennett.BrowserSwitch";
    private const string Ask = "Preguntar siempre";
    private const int HandlesCommandLine = 1 << 3;
    private readonly Preferences preferences = new();
    private readonly string iconPath = Path.Combine(AppContext.BaseDirectory, "browserswitch.png");
    private readonly nint application = g_application_new(ApplicationId, HandlesCommandLine);
    private bool gtkInitialized;
    private nint settingsDialog;

    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("Este proyecto de BrowserSwitch requiere Linux.");
            return 1;
        }
        return new Program().Run(args);
    }

    private int Run(string[] args)
    {
        g_set_prgname(ApplicationId);
        Connect(application, "command-line", (CommandLine)HandleCommand);
        // GLib expects argv[0] and a null terminator, neither of which is in .NET's args.
        string?[] argv = [ApplicationId, .. args, null];
        try { return g_application_run(application, argv.Length - 1, argv); }
        finally { g_object_unref(application); }
    }

    private int HandleCommand(nint app, nint command, nint data)
    {
        var argv = g_application_command_line_get_arguments(command, out var argc);
        try
        {
            if (argc > 2)
                throw new ArgumentException("Uso: BrowserSwitch [enlace | --tray | --quit]");

            var argument = argc == 2 ? Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(argv, nint.Size)) : null;
            switch (argument)
            {
                case null:
                    StartTray();
                    ShowSettings();
                    break;
                case "--tray":
                    StartTray();
                    break;
                case "--quit":
                    g_application_quit(application);
                    break;
                default:
                    OpenLink(DesktopBrowsers.NormalizeWebLink(argument));
                    break;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally { g_strfreev(argv); }
    }

    private void OpenLink(string link)
    {
        var browsers = DesktopBrowsers.Detect();
        var selected = preferences.Load().Preferred(browsers);
        if (selected != null)
        {
            try
            {
                DesktopBrowsers.Open(selected, link);
                return;
            }
            catch (Win32Exception) { }
        }
        ShowChooser(link, browsers, selected == null
            ? "Elige el navegador para este enlace."
            : $"No se pudo iniciar {selected.Name}. Elige un navegador.");
    }

    private void EnsureGtk()
    {
        if (gtkInitialized) return;
        if (gtk_init_check(0, 0) == 0)
            throw new InvalidOperationException("BrowserSwitch necesita una sesión gráfica para mostrar esta opción.");
        gtk_window_set_default_icon_from_file(iconPath, 0);
        gtkInitialized = true;
    }

    private void ShowSettings()
    {
        if (settingsDialog != 0)
        {
            gtk_window_present(settingsDialog);
            return;
        }

        var browsers = DesktopBrowsers.Detect();
        var settings = preferences.Load();
        RefreshTray(settings.PreferredBrowserId, browsers);
        var selected = settings.Preferred(browsers);
        var current = selected?.Name ?? Ask;
        var message = $"Preferencia actual: {current}. Elige qué hacer con los próximos enlaces.";
        if (browsers.Count == 0)
            message = "Instala Firefox, Google Chrome o Chromium para abrir enlaces.";
        else if (selected == null && settings.PreferredBrowserId.Length != 0)
            message = "El navegador guardado no está disponible. Elige qué hacer con los próximos enlaces.";

        settingsDialog = CreateDialog("BrowserSwitch · Preferencias", message, out _);
        var dialog = settingsDialog;
        gtk_dialog_add_button(dialog, "Cerrar", -7);
        gtk_dialog_add_button(dialog, Ask, 1);
        for (var i = 0; i < browsers.Count; i++)
            gtk_dialog_add_button(dialog, browsers[i].Name, i + 2);
        Connect(dialog, "response", (DialogResponse)((_, response, _) =>
        {
            if (response > 0 && !SavePreference(response == 1 ? "" : browsers[response - 2].Id))
                return;
            gtk_widget_destroy(dialog);
        }));
        Present(dialog);
    }

    private void ShowChooser(string link, List<BrowserDefinition> browsers, string message)
    {
        var dialog = CreateDialog("BrowserSwitch · Abrir enlace", browsers.Count == 0
            ? "Instala Firefox, Google Chrome o Chromium para abrir este enlace." : message, out var label);
        var linkLabel = gtk_label_new(link);
        gtk_label_set_ellipsize(linkLabel, 2); // PANGO_ELLIPSIZE_MIDDLE
        gtk_label_set_selectable(linkLabel, 1);
        gtk_box_pack_start(gtk_dialog_get_content_area(dialog), linkLabel, 0, 1, 6);
        gtk_dialog_add_button(dialog, "Cancelar", -6);
        for (var i = 0; i < browsers.Count; i++)
            gtk_dialog_add_button(dialog, browsers[i].Name, i + 1);
        Connect(dialog, "response", (DialogResponse)((_, response, _) =>
        {
            if (response > 0)
            {
                var browser = browsers[response - 1];
                try { DesktopBrowsers.Open(browser, link); }
                catch (Win32Exception)
                {
                    gtk_label_set_text(label, $"No se pudo iniciar {browser.Name}. Elige otro navegador.");
                    return;
                }
            }
            gtk_widget_destroy(dialog);
        }));
        Present(dialog);
    }

    private nint CreateDialog(string title, string message, out nint label)
    {
        EnsureGtk();
        var dialog = gtk_dialog_new();
        gtk_window_set_title(dialog, title);
        gtk_window_set_default_size(dialog, 440, -1);
        var content = gtk_dialog_get_content_area(dialog);
        gtk_container_set_border_width(content, 16);
        label = gtk_label_new(message);
        gtk_label_set_line_wrap(label, 1);
        gtk_box_pack_start(content, label, 0, 1, 0);

        g_application_hold(application);
        Connect(dialog, "destroy", (WidgetSignal)((_, _) =>
        {
            if (settingsDialog == dialog) settingsDialog = 0;
            g_application_release(application);
        }));
        return dialog;
    }

    private static void Present(nint dialog)
    {
        gtk_widget_show_all(dialog);
        gtk_window_present(dialog);
    }

    private bool SavePreference(string id)
    {
        try { preferences.Save(id); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"No se pudo guardar la preferencia. {ex.Message}");
            return false;
        }

        RefreshTray(id);
        return true;
    }

    private void ShowError(string message)
    {
        var dialog = CreateDialog("BrowserSwitch", message, out _);
        gtk_dialog_add_button(dialog, "Cerrar", -7);
        Connect(dialog, "response", (DialogResponse)((_, _, _) => gtk_widget_destroy(dialog)));
        Present(dialog);
    }
}
