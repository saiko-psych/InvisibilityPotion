using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace InvisibilityPotion
{
    public static partial class PatchHealth
    {
        /// <summary>Every "Type.Method" that currently carries at least one patch owned by <paramref name="harmony"/>.</summary>
        public static IReadOnlyList<string> PatchedTargets(Harmony harmony)
        {
            return harmony.GetPatchedMethods()
                .Where(m => Harmony.GetPatchInfo(m)?.Owners.Contains(harmony.Id) == true)
                .Select(m => TargetKey(m.DeclaringType?.Name ?? "?", m.Name))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Logs one error per expected target that did not get patched. Returns the missing list. <paramref name="notEffective"/>: targets
        /// Harmony reports as patched although the patch does nothing (a transpiler that fell back to the original IL); they count as missing.
        /// </summary>
        public static IReadOnlyList<string> Report(Harmony harmony, IEnumerable<string> expectedTargets, IEnumerable<string> notEffective = null)
        {
            var patched = PatchedTargets(harmony);
            if (notEffective != null)
            {
                var drop = new HashSet<string>(notEffective);
                patched = patched.Where(t => !drop.Contains(t)).ToList();
            }
            var missing = MissingTargets(expectedTargets, patched);
            foreach (var target in missing)
            {
                Plugin.Log.LogError($"Patch missing: {target}. The game may have changed; this feature is disabled.");
            }
            Plugin.Log.LogInfo($"Patch health: {patched.Count} targets patched, {missing.Count} missing");
            return missing;
        }
    }
}
