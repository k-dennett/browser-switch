using System.Windows.Forms;

namespace BrowserSwitch.Windows
{
    // A tray popup is transient UI, never an application window on the taskbar.
    // Apply the styles at handle creation, before the shell can create a blank button.
    internal sealed class TrayContextMenu : ContextMenuStrip
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW: excludes taskbar and Alt+Tab.
                parameters.ExStyle &= ~0x00040000; // WS_EX_APPWINDOW: never force a taskbar button.
                return parameters;
            }
        }
    }
}