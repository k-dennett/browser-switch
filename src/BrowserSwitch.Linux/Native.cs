#nullable enable
using System.Runtime.InteropServices;

namespace BrowserSwitch.Linux;

// The Ubuntu GTK 3, GLib and Ayatana C ABIs keep the package free of managed UI dependencies.
internal static class Native
{
    private const string Gio = "libgio-2.0.so.0";
    private const string Glib = "libglib-2.0.so.0";
    private const string GObject = "libgobject-2.0.so.0";
    private const string Gtk = "libgtk-3.so.0";
    internal const string Ayatana = "libayatana-appindicator3.so.1";
    private const CallingConvention Cdecl = CallingConvention.Cdecl;

    [UnmanagedFunctionPointer(Cdecl)] internal delegate int CommandLine(nint app, nint command, nint data);
    [UnmanagedFunctionPointer(Cdecl)] internal delegate void WidgetSignal(nint widget, nint data);
    [UnmanagedFunctionPointer(Cdecl)] internal delegate void DialogResponse(nint dialog, int response, nint data);
    [UnmanagedFunctionPointer(Cdecl)] private delegate void ClosureNotify(nint data, nint closure);

    private static readonly ClosureNotify ReleaseDelegate = (data, _) => GCHandle.FromIntPtr(data).Free();

    internal static void Connect(nint instance, string signal, Delegate callback)
    {
        var root = GCHandle.Alloc(callback);
        g_signal_connect_data(instance, signal, Marshal.GetFunctionPointerForDelegate(callback),
            GCHandle.ToIntPtr(root), ReleaseDelegate, 0);
    }

    [DllImport(GObject, CallingConvention = Cdecl)]
    private static extern nuint g_signal_connect_data(nint instance, string signal, nint handler,
        nint data, ClosureNotify destroy, int flags);
    [DllImport(GObject, CallingConvention = Cdecl)] internal static extern void g_object_unref(nint instance);
    [DllImport(Glib, CallingConvention = Cdecl)] internal static extern void g_strfreev(nint vector);
    [DllImport(Glib, CallingConvention = Cdecl)] internal static extern void g_set_prgname(string name);

    [DllImport(Gio, CallingConvention = Cdecl)] internal static extern nint g_application_new(string id, int flags);
    [DllImport(Gio, CallingConvention = Cdecl)]
    internal static extern int g_application_run(nint app, int argc,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string?[] argv);
    [DllImport(Gio, CallingConvention = Cdecl)] internal static extern void g_application_hold(nint app);
    [DllImport(Gio, CallingConvention = Cdecl)] internal static extern void g_application_release(nint app);
    [DllImport(Gio, CallingConvention = Cdecl)] internal static extern void g_application_quit(nint app);
    [DllImport(Gio, CallingConvention = Cdecl)]
    internal static extern nint g_application_command_line_get_arguments(nint command, out int argc);

    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern int gtk_init_check(nint argc, nint argv);
    [DllImport(Gtk, CallingConvention = Cdecl)]
    internal static extern int gtk_window_set_default_icon_from_file(string path, nint error);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_window_set_title(nint window, string title);
    [DllImport(Gtk, CallingConvention = Cdecl)]
    internal static extern void gtk_window_set_default_size(nint window, int width, int height);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_window_present(nint window);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern nint gtk_dialog_new();
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern nint gtk_dialog_get_content_area(nint dialog);
    [DllImport(Gtk, CallingConvention = Cdecl)]
    internal static extern nint gtk_dialog_add_button(nint dialog, string caption, int response);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern nint gtk_label_new(string text);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_label_set_text(nint label, string text);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_label_set_line_wrap(nint label, int wrap);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_label_set_selectable(nint label, int selectable);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_label_set_ellipsize(nint label, int mode);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_container_set_border_width(nint container, uint width);
    [DllImport(Gtk, CallingConvention = Cdecl)]
    internal static extern void gtk_box_pack_start(nint box, nint widget, int expand, int fill, uint padding);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_widget_show_all(nint widget);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_widget_destroy(nint widget);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_widget_set_sensitive(nint widget, int sensitive);

    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern nint gtk_menu_new();
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern nint gtk_menu_item_new_with_label(string label);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern nint gtk_separator_menu_item_new();
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_menu_shell_append(nint menu, nint child);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern nint gtk_check_menu_item_new_with_label(string label);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_check_menu_item_set_active(nint item, int active);
    [DllImport(Gtk, CallingConvention = Cdecl)] internal static extern void gtk_check_menu_item_set_draw_as_radio(nint item, int radio);

    [DllImport(Ayatana, CallingConvention = Cdecl)]
    internal static extern nint app_indicator_new(string id, string icon, int category);
    [DllImport(Ayatana, CallingConvention = Cdecl)]
    internal static extern void app_indicator_set_menu(nint indicator, nint menu);
    [DllImport(Ayatana, CallingConvention = Cdecl)]
    internal static extern void app_indicator_set_status(nint indicator, int status);
    [DllImport(Ayatana, CallingConvention = Cdecl)]
    internal static extern void app_indicator_set_title(nint indicator, string title);
}
