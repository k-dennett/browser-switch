import AppKit

@MainActor
struct BrowserCatalog {
    private let workspace = NSWorkspace.shared
    private let ownBundle = Bundle.main

    func installedBrowsers(for target: URL? = nil) -> [Browser] {
        let http = URL(string: "http://example.com/")!
        let https = URL(string: "https://example.com/")!
        var eligiblePaths = Set(workspace.urlsForApplications(toOpen: http).map(canonicalPath))
        if let target, target.isFileURL {
            eligiblePaths.formIntersection(workspace.urlsForApplications(toOpen: target).map(canonicalPath))
        }
        let ownPath = canonicalPath(ownBundle.bundleURL)

        let candidates = workspace.urlsForApplications(toOpen: https).compactMap { appURL -> Browser? in
            let path = canonicalPath(appURL)
            guard path != ownPath, eligiblePaths.contains(path),
                  let bundle = Bundle(url: appURL), let id = bundle.bundleIdentifier,
                  let executable = bundle.executableURL,
                  FileManager.default.isExecutableFile(atPath: executable.path) else { return nil }

            let name = (bundle.object(forInfoDictionaryKey: "CFBundleDisplayName") as? String)
                ?? appURL.deletingPathExtension().lastPathComponent
            return Browser(id: id, name: name, applicationURL: appURL)
        }
        return Browser.choices(from: candidates, excluding: ownBundle.bundleIdentifier ?? "")
    }

    private func canonicalPath(_ url: URL) -> String {
        url.resolvingSymlinksInPath().standardizedFileURL.path
    }
}
