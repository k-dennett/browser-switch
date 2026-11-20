import AppKit

@main
@MainActor
struct BrowserSwitchApp {
    static func main() {
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.delegate = delegate
        withExtendedLifetime(delegate) {
            app.run()
        }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private let catalog = BrowserCatalog()
    private let preference = BrowserPreference(defaults: .standard)
    private let askAlways = "Preguntar siempre"
    private var statusItem: NSStatusItem?
    private let preferencePicker = NSPopUpButton(frame: .zero, pullsDown: false)
    private lazy var preferencesWindow = makePreferencesWindow()
    private var pendingLinks: [URL] = []
    private var didFinishLaunching = false
    private var routingLink = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        if let url = Bundle.main.url(forResource: "BrowserSwitch", withExtension: "png"),
           let icon = NSImage(contentsOf: url) {
            icon.size = NSSize(width: 18, height: 18)
            item.button?.image = icon
        } else {
            item.button?.title = "BS"
        }
        item.button?.toolTip = "BrowserSwitch"
        item.button?.setAccessibilityLabel("BrowserSwitch")

        let menu = NSMenu()
        menu.delegate = self
        menuNeedsUpdate(menu)
        item.menu = menu
        statusItem = item
        didFinishLaunching = true
        scheduleNextLink()
    }

    func applicationShouldOpenUntitledFile(_ sender: NSApplication) -> Bool {
        true
    }

    func applicationOpenUntitledFile(_ sender: NSApplication) -> Bool {
        // Finder sends an open-application event when there is no URL to handle.
        DispatchQueue.main.async {
            if self.pendingLinks.isEmpty && !self.routingLink {
                self.showPreferences()
            }
        }
        return true
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows: Bool) -> Bool {
        showPreferences()
        return false
    }

    func application(_ application: NSApplication, open urls: [URL]) {
        pendingLinks.append(contentsOf: urls.filter(canRoute))
        scheduleNextLink()
    }

    func menuNeedsUpdate(_ menu: NSMenu) {
        let browsers = catalog.installedBrowsers()
        let current = preference.preferred(in: browsers)?.id
        menu.removeAllItems()
        for browser in browsers {
            addPreference(to: menu, title: browser.name, id: browser.id, active: current)
        }
        addPreference(to: menu, title: askAlways, id: nil, active: current)
        menu.addItem(.separator())
        menu.addItem(withTitle: "Preferencias…", action: #selector(showPreferences), keyEquivalent: ",")
            .target = self
        menu.addItem(withTitle: "Salir de BrowserSwitch", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
            .target = NSApp
    }

    private func addPreference(to menu: NSMenu, title: String, id: String?, active: String?) {
        let item = menu.addItem(withTitle: title, action: #selector(changePreference(_:)), keyEquivalent: "")
        item.target = self
        item.representedObject = id
        item.state = active == id ? .on : .off
    }

    @objc private func changePreference(_ sender: NSMenuItem) {
        savePreference(sender.representedObject as? String)
    }

    @objc private func changePreferenceFromWindow(_ sender: NSPopUpButton) {
        savePreference(sender.selectedItem?.representedObject as? String)
    }

    private func savePreference(_ browserID: String?) {
        preference.select(browserID)
        refreshPreferencePicker()
    }

    @objc private func showPreferences() {
        refreshPreferencePicker()
        let window = preferencesWindow
        if !window.isVisible { window.center() }
        activate()
        window.makeKeyAndOrderFront(nil)
    }

    private func refreshPreferencePicker() {
        let browsers = catalog.installedBrowsers()
        let current = preference.preferred(in: browsers)?.id
        preferencePicker.removeAllItems()
        preferencePicker.addItem(withTitle: askAlways)
        for browser in browsers { append(browser, to: preferencePicker) }
        let index = browsers.firstIndex { $0.id == current }.map { $0 + 1 } ?? 0
        preferencePicker.selectItem(at: index)
    }

    private func makePreferencesWindow() -> NSWindow {
        let window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 400, height: 138),
            styleMask: [.titled, .closable], backing: .buffered, defer: false
        )
        window.title = "BrowserSwitch"
        window.isReleasedWhenClosed = false

        let label = NSTextField(labelWithString: "Navegador para los próximos enlaces")
        preferencePicker.target = self
        preferencePicker.action = #selector(changePreferenceFromWindow(_:))
        preferencePicker.setAccessibilityLabel(label.stringValue)
        let hint = NSTextField(wrappingLabelWithString:
            "Para recibir enlaces externos, elige BrowserSwitch como navegador predeterminado en Escritorio y Dock.")
        hint.textColor = .secondaryLabelColor
        hint.font = .systemFont(ofSize: NSFont.smallSystemFontSize)

        let stack = NSStackView(views: [label, preferencePicker, hint])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 8
        stack.translatesAutoresizingMaskIntoConstraints = false
        let content = NSView()
        window.contentView = content
        content.addSubview(stack)
        NSLayoutConstraint.activate([
            stack.topAnchor.constraint(equalTo: content.topAnchor, constant: 18),
            stack.leadingAnchor.constraint(equalTo: content.leadingAnchor, constant: 20),
            stack.trailingAnchor.constraint(equalTo: content.trailingAnchor, constant: -20),
            preferencePicker.widthAnchor.constraint(equalTo: stack.widthAnchor),
            hint.widthAnchor.constraint(equalTo: stack.widthAnchor)
        ])
        window.initialFirstResponder = preferencePicker
        return window
    }

    private func scheduleNextLink() {
        DispatchQueue.main.async { self.routeNextLink() }
    }

    private func routeNextLink() {
        guard didFinishLaunching, !routingLink, !pendingLinks.isEmpty else { return }
        routingLink = true
        let link = pendingLinks.removeFirst()
        let browsers = catalog.installedBrowsers(for: link)
        if let browser = preference.preferred(in: browsers) {
            open(link, in: browser, isFixed: true)
        } else {
            let note = preference.fixedBrowserID == nil ? nil : "El navegador guardado no está disponible."
            ask(for: link, browsers: browsers, note: note)
        }
    }

    private func ask(for link: URL, browsers: [Browser], note: String?) {
        let alert = NSAlert()
        alert.messageText = "¿Con qué navegador abrir este enlace?"
        let address = link.absoluteString
        let displayAddress = address.count > 210 ? "\(address.prefix(150))…\(address.suffix(60))" : address
        if browsers.isEmpty {
            alert.informativeText = [note, "No hay navegadores disponibles.", displayAddress]
                .compactMap { $0 }.joined(separator: "\n\n")
            alert.addButton(withTitle: "Cerrar").keyEquivalent = "\u{1b}"
            activate()
            alert.runModal()
            finishLink()
            return
        }

        alert.informativeText = [note, displayAddress].compactMap { $0 }.joined(separator: "\n\n")
        let picker = NSPopUpButton(frame: NSRect(x: 0, y: 0, width: 300, height: 28), pullsDown: false)
        picker.setAccessibilityLabel("Navegador para este enlace")
        for browser in browsers { append(browser, to: picker) }
        picker.selectItem(at: 0)
        alert.accessoryView = picker
        alert.addButton(withTitle: "Abrir")
        alert.addButton(withTitle: "Cancelar").keyEquivalent = "\u{1b}"
        activate()
        if alert.runModal() == .alertFirstButtonReturn,
           let id = picker.selectedItem?.representedObject as? String,
           let browser = browsers.first(where: { $0.id == id }) {
            open(link, in: browser, isFixed: false)
        } else {
            finishLink()
        }
    }

    private func open(_ link: URL, in browser: Browser, isFixed: Bool) {
        let configuration = NSWorkspace.OpenConfiguration()
        configuration.activates = true
        NSWorkspace.shared.open([link], withApplicationAt: browser.applicationURL, configuration: configuration) {
            _, error in
            DispatchQueue.main.async {
                if let error {
                    if isFixed {
                        let alternatives = self.catalog.installedBrowsers(for: link).filter { $0.id != browser.id }
                        self.ask(for: link, browsers: alternatives, note: "No se pudo abrir el enlace en \(browser.name).")
                        return
                    }
                    let alert = NSAlert()
                    alert.messageText = "No se pudo abrir el enlace en \(browser.name)."
                    alert.informativeText = error.localizedDescription
                    alert.addButton(withTitle: "Cerrar")
                    self.activate()
                    alert.runModal()
                }
                self.finishLink()
            }
        }
    }

    private func finishLink() {
        routingLink = false
        scheduleNextLink()
    }

    private func append(_ browser: Browser, to picker: NSPopUpButton) {
        let title = picker.item(withTitle: browser.name) == nil ? browser.name : "\(browser.name) (\(browser.id))"
        picker.addItem(withTitle: title)
        picker.lastItem?.representedObject = browser.id
    }

    private func activate() {
        if #available(macOS 14, *) {
            NSApp.activate()
        } else {
            NSApp.activate(ignoringOtherApps: true)
        }
    }
}
