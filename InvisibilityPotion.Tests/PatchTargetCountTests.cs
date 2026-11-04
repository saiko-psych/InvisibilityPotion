using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace InvisibilityPotion.Tests
{
    /// <summary>
    /// The number of Harmony targets is written into the player docs ("Patch health: N targets patched"). Counted here from
    /// Plugin.ExpectedPatchTargets (entries inside #if DEBUG are Debug-only), so adding a patch without updating the docs fails the
    /// tests (the Package README said 25 for a Release build that patches 23 until 2026-11-04). Reads the files, no game types.
    /// </summary>
    public class PatchTargetCountTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "InvisibilityPotion.sln"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        private static string Read(string relative) => File.ReadAllText(Path.Combine(RepoRoot(), relative));

        /// <summary>(Release, Debug) target counts from the ExpectedPatchTargets initializer.</summary>
        internal static (int release, int debug) Count()
        {
            var plugin = Read("InvisibilityPotion/Plugin.cs");
            var start = plugin.IndexOf("ExpectedPatchTargets = new List<string>", System.StringComparison.Ordinal);
            Assert.True(start >= 0, "ExpectedPatchTargets initializer not found in Plugin.cs");
            var end = plugin.IndexOf("};", start, System.StringComparison.Ordinal);
            var release = 0;
            var debugOnly = 0;
            var inDebug = false;
            foreach (var raw in plugin.Substring(start, end - start).Split('\n'))
            {
                var line = raw.Trim();
                if (line.StartsWith("#if DEBUG")) { inDebug = true; continue; }
                if (line.StartsWith("#endif")) { inDebug = false; continue; }
                Assert.False(line.StartsWith("#else") || line.StartsWith("#elif"), "only #if DEBUG ... #endif is understood here");
                var n = Regex.Matches(line, @"PatchHealth\.TargetKey\(").Count;
                if (inDebug) debugOnly += n; else release += n;
            }
            return (release, release + debugOnly);
        }

        [Fact]
        public void Counts_are_plausible() => Assert.InRange(Count().release, 1, 200);

        [Theory]
        [InlineData("InvisibilityPotion/Package/README.md")]
        [InlineData("docs/wiki/01-Installation.md")]
        [InlineData("docs/wiki/05-Multiplayer-and-Server.md")]
        public void Player_docs_name_the_release_count(string file)
        {
            var (release, _) = Count();
            Assert.Contains($"Patch health: {release} targets patched, 0 missing", Read(file));
        }

        [Fact]
        public void Compatibility_doc_names_both_counts()
        {
            var (release, debug) = Count();
            Assert.Contains($"{release} targets in a Release build, {debug} in Debug", Read("docs/compatibility.md"));
        }

        [Fact]
        public void Release_checklist_names_both_counts()
        {
            var (release, debug) = Count();
            var text = Read("docs/release.md");
            Assert.Contains($"Patch health: {debug} targets patched, 0 missing` in Debug", text);
            Assert.Contains($"Patch health: {release} targets patched, 0 missing`", text);
        }
    }
}
