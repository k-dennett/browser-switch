import Foundation

struct Browser {
    let id: String
    let name: String
    let applicationURL: URL

    static func choices(from candidates: [Browser], excluding ownID: String) -> [Browser] {
        var seen = Set([ownID.lowercased()])
        return candidates.filter { seen.insert($0.id.lowercased()).inserted }
            .sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
    }
}

struct BrowserPreference {
    private static let key = "PreferredBrowserID"
    let defaults: UserDefaults

    var fixedBrowserID: String? {
        defaults.string(forKey: Self.key)
    }

    func select(_ browserID: String?) {
        if let browserID {
            defaults.set(browserID, forKey: Self.key)
        } else {
            defaults.removeObject(forKey: Self.key)
        }
    }

    func preferred(in browsers: [Browser]) -> Browser? {
        guard let fixedBrowserID else { return nil }
        return browsers.first { $0.id.caseInsensitiveCompare(fixedBrowserID) == .orderedSame }
    }
}

func canRoute(_ url: URL) -> Bool {
    if url.isFileURL {
        return ["html", "htm", "xhtml", "xht"].contains(url.pathExtension.lowercased())
    }

    guard let components = URLComponents(url: url, resolvingAgainstBaseURL: true),
          let scheme = components.scheme?.lowercased(),
          let host = components.host, !host.isEmpty else { return false }
    return scheme == "http" || scheme == "https"
}
