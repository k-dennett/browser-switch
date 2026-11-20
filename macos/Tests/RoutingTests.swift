import Foundation

@main
struct RoutingTests {
    static func main() throws {
        let arguments = CommandLine.arguments
        if arguments.count == 3 {
            let defaults = UserDefaults(suiteName: arguments[1])!
            BrowserPreference(defaults: defaults).select(arguments[2])
            // Make the separate test process's write observable immediately.
            precondition(defaults.synchronize())
            return
        }

        var assertions = 0
        func expect(_ condition: @autoclosure () -> Bool, _ message: String) {
            precondition(condition(), message)
            assertions += 1
        }

        // Cases adapted from TestTargets and TestProcess in tests/BrowserSwitch.Tests.cs.
        let encoded = "https://example.com/a%20b?q=%22quoted%22&x=%25PATH%25&y=a%7Cb#%C3%B1"
        let link = URL(string: encoded)!
        expect(canRoute(link), "Accept an HTTPS URL containing spaces, punctuation and Unicode (encoded).")
        expect(link.absoluteString == encoded, "Preserve the complete URL for the destination browser.")
        expect(canRoute(URL(string: "http://example.com/")!), "Accept HTTP.")
        expect(canRoute(URL(string: "HTTPS://example.com/")!), "Accept uppercase schemes.")
        expect(canRoute(URL(fileURLWithPath: "/tmp/example.html")), "Keep the HTML case from Windows.")
        expect(canRoute(URL(fileURLWithPath: "/tmp/example.HTM")), "Accept uppercase HTML extensions.")
        expect(canRoute(URL(fileURLWithPath: "/tmp/example.xhtml")), "Accept XHTML declared for Launch Services.")
        for invalid in ["javascript:alert(1)", "file:///tmp/example.pdf", "file:///C:/bad.exe",
                        "ftp://example.com", "--register", "https:/example.com", "https:///path", ""] {
            expect(URL(string: invalid).map(canRoute) != true, "Reject unsupported target: \(invalid)")
        }

        // Cases adapted from TestDiscoveryAndMerge and TestSettings.
        let ownID = "io.github.k-dennett.BrowserSwitch"
        let own = Browser(id: ownID.lowercased(), name: "BrowserSwitch", applicationURL: bundle("BrowserSwitch"))
        let safari = Browser(id: "com.apple.Safari", name: "Safari", applicationURL: bundle("Safari"))
        let chrome = Browser(id: "com.google.Chrome", name: "Google Chrome", applicationURL: bundle("Google Chrome"))
        let firefox = Browser(id: "org.mozilla.firefox", name: "Firefox", applicationURL: bundle("Firefox"))
        let duplicate = Browser(id: "COM.GOOGLE.CHROME", name: "Chrome copy", applicationURL: bundle("Chrome copy"))
        let choices = Browser.choices(from: [own, safari, chrome, firefox, duplicate], excluding: ownID)
        expect(choices.map(\.id) == [firefox.id, chrome.id, safari.id], "Exclude self, deduplicate IDs and sort choices.")
        expect(choices.first(where: { $0.id == chrome.id })?.applicationURL == chrome.applicationURL,
               "Keep the first registered copy of a browser.")

        let suite = "BrowserSwitch.RoutingTests." + UUID().uuidString
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        let preference = BrowserPreference(defaults: defaults)
        expect(preference.fixedBrowserID == nil, "Start in Ask Always.")
        preference.select(safari.id)
        expect(preference.preferred(in: choices)?.id == safari.id, "Route the next link to Safari.")
        preference.select(chrome.id.lowercased())
        let reloaded = BrowserPreference(defaults: UserDefaults(suiteName: suite)!)
        expect(reloaded.preferred(in: choices)?.id == chrome.id, "Persist a switch and resolve IDs case-insensitively.")
        expect(reloaded.preferred(in: [safari, firefox]) == nil, "A missing fixed browser falls back to asking.")
        expect(reloaded.fixedBrowserID == chrome.id.lowercased(), "Fallback keeps the stored browser ID.")
        reloaded.select(nil)
        expect(preference.preferred(in: choices) == nil && preference.fixedBrowserID == nil,
               "Ask Always clears the fixed browser.")

        let processSuite = suite + ".Process"
        let writer = Process()
        writer.executableURL = URL(fileURLWithPath: arguments[0])
        writer.arguments = [processSuite, firefox.id]
        try writer.run()
        writer.waitUntilExit()
        let persisted = UserDefaults(suiteName: processSuite)!
        defer { persisted.removePersistentDomain(forName: processSuite) }
        expect(writer.terminationStatus == 0 && BrowserPreference(defaults: persisted).fixedBrowserID == firefox.id,
               "Restore a fixed selection written by another process.")

        print("\(assertions) comprobaciones de enrutamiento correctas.")
    }

    private static func bundle(_ name: String) -> URL {
        URL(fileURLWithPath: "/Applications/\(name).app", isDirectory: true)
    }
}
