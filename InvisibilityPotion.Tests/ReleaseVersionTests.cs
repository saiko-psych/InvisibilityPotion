using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace InvisibilityPotion.Tests
{
    /// <summary>
    /// The version lives in Plugin.cs, the csproj and manifest.json (CLAUDE.md: bump them together), and the newest CHANGELOG entry
    /// must name it. The Jötunn version in the csproj and the manifest dependency must match too: Jötunn refuses a client whose
    /// patch version differs from the server's (docs/compatibility.md §7). Reads the repository files, no game types.
    /// </summary>
    public class ReleaseVersionTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "InvisibilityPotion.sln"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(RepoRoot(), Path.Combine(parts)));

        private static string Match(string text, string pattern)
        {
            var m = Regex.Match(text, pattern, RegexOptions.Multiline);
            Assert.True(m.Success, $"pattern not found: {pattern}");
            return m.Groups[1].Value;
        }

        private static string PluginVersion() =>
            Match(Read("InvisibilityPotion", "Plugin.cs"), @"PluginVersion\s*=\s*""([^""]+)""");

        [Fact]
        public void CsprojVersion_MatchesPlugin()
        {
            Assert.Equal(PluginVersion(), Match(Read("InvisibilityPotion", "InvisibilityPotion.csproj"), @"<Version>([^<]+)</Version>"));
        }

        [Fact]
        public void ManifestVersion_MatchesPlugin()
        {
            Assert.Equal(PluginVersion(), Match(Read("InvisibilityPotion", "Package", "manifest.json"), @"""version_number""\s*:\s*""([^""]+)"""));
        }

        [Fact]
        public void NewestChangelogEntry_IsPluginVersion()
        {
            Assert.Equal(PluginVersion(), Match(Read("InvisibilityPotion", "Package", "CHANGELOG.md"), @"^## (\d+\.\d+\.\d+)"));
        }

        [Fact]
        public void ManifestJotunnDependency_MatchesCsproj()
        {
            var csproj = Match(Read("InvisibilityPotion", "InvisibilityPotion.csproj"), @"<JotunnVersion[^>]*>([^<]+)</JotunnVersion>");
            var manifest = Match(Read("InvisibilityPotion", "Package", "manifest.json"), @"""ValheimModding-Jotunn-([^""]+)""");
            Assert.Equal(csproj, manifest);
        }
    }
}
