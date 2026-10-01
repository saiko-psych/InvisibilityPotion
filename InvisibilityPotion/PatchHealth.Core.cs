using System.Collections.Generic;
using System.Linq;

namespace InvisibilityPotion
{
    /// <summary>
    /// Pure logic for the startup patch health check. No game or Harmony types here so it can be unit-tested.
    /// </summary>
    public static partial class PatchHealth
    {
        public static string TargetKey(string typeName, string methodName) => typeName + "." + methodName;

        /// <summary>Expected targets that do not appear in <paramref name="patched"/>, in the expected order.</summary>
        public static IReadOnlyList<string> MissingTargets(IEnumerable<string> expected, IEnumerable<string> patched)
        {
            var patchedSet = new HashSet<string>(patched);
            return expected.Where(t => !patchedSet.Contains(t)).ToList();
        }
    }
}
