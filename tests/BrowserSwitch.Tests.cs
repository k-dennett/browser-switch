using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;
using BrowserSwitch.Core;
using BrowserSwitch.Windows;
using Microsoft.Win32;

internal static class Tests
{
    static int assertions;
    static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }
    static void Reject(Action action, string message)
    {
        bool failed = false;
        try { action(); } catch (ArgumentException) { failed = true; }
        Assert(failed, message);
    }
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--capture")
        {
            using (StreamWriter writer = new StreamWriter(args[1])) new XmlSerializer(typeof(string[])).Serialize(writer, args.Skip(2).ToArray());
            return 0;
        }
        string temp = Path.Combine(Path.GetTempPath(), "BrowserSwitch tests " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            TestApplicationIcon(temp);
            TestTrayMenu();
            TestArguments();
            TestTargets();
            TestSettings(temp);
            TestDiscoveryAndMerge();
            TestProcess(temp);
            TestRegistryDiscovery();
            Console.WriteLine("PASS: " + assertions + " assertions, including a real child-process argv round trip and HKCU registry discovery.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(temp, true); }
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    static extern int GetWindowLong(IntPtr handle, int index);
    static void TestTrayMenu()
    {
        using (ContextMenuStrip menu = new TrayContextMenu())
        {
            menu.Items.Add("BrowserSwitch popup regression test");
            for (int attempt = 0; attempt < 2; attempt++)
            {
                menu.Show(new Point(0, 0));
                Application.DoEvents();
                int style = GetWindowLong(menu.Handle, -20);
                Assert(menu.Visible, "Tray menu must remain visible.");
                Assert((style & 0x00040000) == 0, "Tray popup must not request its own taskbar button.");
                Assert((style & 0x00000080) != 0, "Tray popup must use WS_EX_TOOLWINDOW to stay out of the taskbar and Alt+Tab.");
                menu.Close();
                Application.DoEvents();
                Assert(!menu.Visible, "Closing the menu must not leave a visible popup.");
            }
        }
    }
    delegate bool ResourceNameCallback(IntPtr module, IntPtr type, IntPtr name, IntPtr state);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadLibraryExW")]
    static extern IntPtr LoadIconResources(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumResourceNamesW")]
    static extern bool EnumResourceNames(IntPtr module, IntPtr type, ResourceNameCallback callback, IntPtr state);
    [DllImport("kernel32.dll")]
    static extern bool FreeLibrary(IntPtr module);
    static void TestApplicationIcon(string temp)
    {
        using (Icon icon = AppIcon.Create())
        using (Bitmap image = icon.ToBitmap())
        {
            Assert(icon.Width == 32 && icon.Height == 32, "Tray icon must load at its requested size from the embedded asset.");
            Color center = image.GetPixel(16, 16);
            Assert(center.A == 255 && center.R == 39 && center.G == 90 && center.B == 196, "Embedded icon must preserve the blue BrowserSwitch design: " + center);
        }
        string iconPath = Path.Combine(temp, "BrowserSwitch.ico");
        AppIcon.Save(iconPath);
        using (BinaryReader reader = new BinaryReader(File.OpenRead(iconPath)))
            Assert(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == 7, "Shortcut asset must preserve all seven ICO resolutions.");
        string executable = Path.Combine(temp, "BrowserSwitch.exe");
        File.Copy(typeof(AppIcon).Assembly.Location, executable);
        IntPtr resources = LoadIconResources(executable, IntPtr.Zero, 2); // LOAD_LIBRARY_AS_DATAFILE: inspect without executing.
        Assert(resources != IntPtr.Zero, "Compiled application resources must be readable by Windows.");
        try
        {
            int groups = 0, images = 0;
            EnumResourceNames(resources, new IntPtr(14), delegate { groups++; return true; }, IntPtr.Zero); // RT_GROUP_ICON
            EnumResourceNames(resources, new IntPtr(3), delegate { images++; return true; }, IntPtr.Zero); // RT_ICON
            Assert(groups >= 1 && images >= 7, "The executable itself must contain a native multi-resolution Windows icon for Explorer and Search.");
        }
        finally { if (resources != IntPtr.Zero) FreeLibrary(resources); }
    }
    static BrowserDefinition Receiver()
    {
        return new BrowserDefinition { Id = "receiver", Name = "Test browser", ExecutablePath = System.Reflection.Assembly.GetExecutingAssembly().Location, IsManual = true };
    }
    static void TestArguments()
    {
        string[] values = new string[] { "", "plain", "two words", "a\"b", "C:\\path with spaces\\", "\\\\\"", "áé 漢字", "& | < > %PATH% $(test)", "--profile-directory=Profile 1" };
        List<string> parsed = CommandLine.Split(CommandLine.Join(values));
        Assert(values.SequenceEqual(parsed), "Quote/parse must preserve all argument boundaries.");
        Random random = new Random(42);
        const string chars = " abcd\\\"é&%|";
        for (int i = 0; i < 250; i++)
        {
            string sample = new string(Enumerable.Range(0, random.Next(0, 50)).Select(delegate(int unused) { return chars[random.Next(chars.Length)]; }).ToArray());
            Assert(CommandLine.Split(CommandLine.Quote(sample)).Single() == sample, "Random Windows argv round trip.");
        }
    }
    static void TestTargets()
    {
        Assert(LinkTarget.Normalize("https://example.com/a b?q=á").Contains("a%20b"), "Normalize spaces.");
        Assert(LinkTarget.Normalize("http://example.com/").StartsWith("http:"), "Keep HTTP.");
        Assert(LinkTarget.Normalize("file:///C:/example.html").EndsWith("example.html"), "Keep legacy HTML support.");
        foreach (string bad in new string[] { "javascript:alert(1)", "file:///C:/bad.exe", "ftp://example.com", "--register", "https://example.com/\r\nextra", "" })
            Reject(delegate { LinkTarget.Normalize(bad); }, "Reject invalid targets: " + bad);
        BrowserDefinition browser = Receiver();
        browser.ExecutablePath = Path.Combine(Path.GetDirectoryName(browser.ExecutablePath), "BrowserSwitch.exe");
        Reject(delegate { BrowserLauncher.Validate(browser); }, "Exclude BrowserSwitch.");
    }
    static void TestSettings(string temp)
    {
        SettingsStore store = new SettingsStore(Path.Combine(temp, "settings.xml"));
        BrowserSettings settings = store.Load();
        Assert(settings.PreferredBrowserId == "", "New install asks always.");
        BrowserDefinition browser = Receiver(); browser.Name = "Navegador ñ";
        settings.ManualBrowsers.Add(browser); settings.PreferredBrowserId = browser.Id;
        store.Save(settings);
        BrowserSettings loaded = store.Load();
        Assert(loaded.ManualBrowsers[0].Name == browser.Name && loaded.Preferred(loaded.ManualBrowsers).Id == browser.Id, "Persist manual browser and fixed mode.");
        loaded.PreferredBrowserId = ""; store.Save(loaded);
        Assert(store.Load().Preferred(store.Load().ManualBrowsers) == null, "Ask always clears fixed mode.");
        Assert(File.Exists(store.FilePath + ".bak"), "Atomic save keeps a previous configuration.");
        loaded.ManualBrowsers.RemoveAt(0); store.Save(loaded);
        Assert(store.Load().ManualBrowsers.Count == 0, "Manual deletion persists.");
        File.WriteAllText(store.FilePath, "<invalid>");
        Assert(store.Load().PreferredBrowserId == "" && store.Warning != null, "Corrupt settings fall back to ask always with warning.");
        Assert(Directory.GetFiles(temp, "settings.xml.corrupt-*").Length == 1, "Corrupt input is preserved.");
        string preference = Path.Combine(temp, "preferencia.txt"); File.WriteAllText(preference, browser.Name);
        SettingsStore migrated = new SettingsStore(Path.Combine(temp, "imported.xml"));
        Assert(migrated.ImportLegacy(preference, new List<BrowserDefinition> { browser }), "Import old preference once.");
        Assert(migrated.Load().PreferredBrowserId == browser.Id, "Imported fixed mode points to detected browser.");
        File.WriteAllText(preference, "Other");
        Assert(!migrated.ImportLegacy(preference, new List<BrowserDefinition> { browser }), "Never overwrite existing settings on update.");
    }
    static void TestDiscoveryAndMerge()
    {
        BrowserDefinition receiver = Receiver();
        BrowserDefinition parsed = BrowserDiscovery.FromCommand("Test", CommandLine.Quote(receiver.ExecutablePath) + " --profile test \"%1\"");
        Assert(parsed != null && parsed.Arguments.SequenceEqual(new string[] { "--profile", "test" }), "Remove registered URL placeholder; preserve fixed arguments.");
        Assert(BrowserDiscovery.FromCommand("Unsupported", CommandLine.Quote(receiver.ExecutablePath) + " --url=%1") == null, "Reject embedded placeholders.");
        Assert(BrowserDiscovery.FromCommand("Self", CommandLine.Quote(Path.Combine(Path.GetDirectoryName(receiver.ExecutablePath), "BrowserSwitch.exe"))) == null, "Exclude registered self command.");
        receiver.Arguments = new List<string>(parsed.Arguments);
        BrowserSettings settings = new BrowserSettings(); settings.ManualBrowsers.Add(receiver);
        List<BrowserDefinition> result = BrowserDiscovery.Merge(new List<BrowserDefinition> { parsed, parsed }, settings);
        Assert(result.Count == 1 && result[0].IsManual, "Manual override and duplicate suppression.");
        settings.HiddenBrowserIds.Add(parsed.Id); settings.ManualBrowsers.Clear();
        Assert(BrowserDiscovery.Merge(new List<BrowserDefinition> { parsed }, settings).Count == 0, "Hide detected browser.");
        settings.HiddenBrowserIds.Clear();
        Assert(BrowserDiscovery.Merge(new List<BrowserDefinition> { parsed }, settings).Count == 1, "Restore detected browser.");
        settings.PreferredBrowserId = "missing";
        Assert(settings.Preferred(result) == null, "Unavailable preference falls back to asking.");
    }
    static void TestProcess(string temp)
    {
        string capture = Path.Combine(temp, "captured arguments.xml");
        BrowserDefinition browser = Receiver();
        string[] fixedArgs = { "two words", "quote\"and\\", "Unicode ñ 漢字", "& | %PATH% $(text)" };
        browser.Arguments.Add("--capture"); browser.Arguments.Add(capture); browser.Arguments.AddRange(fixedArgs);
        string link = "https://example.com/a b?q=\"quoted\"&x=%25PATH%25&y=a|b#ñ";
        ProcessStartInfo start = BrowserLauncher.CreateStartInfo(browser, link);
        Assert(!start.UseShellExecute && start.FileName == browser.ExecutablePath, "Launch directly without shell.");
        using (Process process = Process.Start(start))
        {
            if (!process.WaitForExit(10000)) { process.Kill(); throw new Exception("Receiver timed out."); }
            Assert(process.ExitCode == 0, "Argument receiver completed.");
        }
        string[] received;
        using (StreamReader reader = new StreamReader(capture)) received = (string[])new XmlSerializer(typeof(string[])).Deserialize(reader);
        Assert(received.Length == fixedArgs.Length + 1, "URL must remain one argument.");
        Assert(received.Take(fixedArgs.Length).SequenceEqual(fixedArgs), "Fixed args arrive unchanged.");
        Assert(received.Last() == LinkTarget.Normalize(link), "URL arrives unchanged after normalization.");
    }
    static void TestRegistryDiscovery()
    {
        string id = "BrowserSwitch.Test." + Guid.NewGuid().ToString("N");
        string client = @"Software\Clients\StartMenuInternet\" + id;
        string caps = client + @"\Capabilities";
        string classPath = @"Software\Classes\" + id;
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(client)) key.SetValue("", "BrowserSwitch test receiver");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(client + @"\shell\open\command")) key.SetValue("", CommandLine.Quote(Receiver().ExecutablePath));
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(caps)) key.SetValue("ApplicationName", "BrowserSwitch test receiver");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(caps + @"\URLAssociations")) key.SetValue("https", id);
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(classPath + @"\shell\open\command")) key.SetValue("", CommandLine.Quote(Receiver().ExecutablePath) + " --single-argument \"%1\"");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications")) key.SetValue(id, caps);
            BrowserDiscovery discovery = new BrowserDiscovery();
            List<BrowserDefinition> browsers = discovery.Detect();
            Assert(browsers.Count(delegate(BrowserDefinition b) { return b.ExecutablePath == Receiver().ExecutablePath; }) == 1, "HKCU discovery merges client/capabilities and 32/64-bit duplicates.");
            Assert(!browsers.Any(delegate(BrowserDefinition b) { return BrowserLauncher.IsSelf(b.ExecutablePath); }), "Live discovery excludes self.");
            Assert(browsers.Select(BrowserDiscovery.Fingerprint).Distinct().Count() == browsers.Count, "Live discovery contains no duplicate commands.");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(client, false);
            Registry.CurrentUser.DeleteSubKeyTree(classPath, false);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true)) if (key != null) key.DeleteValue(id, false);
        }
    }
}