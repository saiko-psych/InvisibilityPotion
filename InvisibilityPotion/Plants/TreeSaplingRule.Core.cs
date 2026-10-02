using System.Collections.Generic;

namespace InvisibilityPotion.Plants
{
    /// <summary>
    /// Where Huldra's Hair may be planted (round N ruling 2), no game types: within <see cref="TrunkRadius"/> of the trunk of an
    /// eligible fir or pine, identified by the ZDO prefab hash of the tree's ZNetView. Candidates are the trees whose colliders
    /// overlap a sphere of that radius around the placement point, with the distance measured by the caller.
    /// </summary>
    public static class TreeSaplingRule
    {
        /// <summary>Maximum distance from the placement point to the trunk, metres.</summary>
        public const float TrunkRadius = 1.0f;

        /// <summary>Tree prefabs that accept the sapling (the same trees that may carry wild lichen).</summary>
        public static readonly string[] EligibleTrees = { "FirTree", "Pinetree_01", "FirTree_big" };

        public enum Verdict { Ok, NoTree, Taken }

        public readonly struct Candidate
        {
            public readonly int PrefabHash;
            public readonly float Distance;
            public readonly bool HasLichen;

            public Candidate(int prefabHash, float distance, bool hasLichen)
            {
                PrefabHash = prefabHash;
                Distance = distance;
                HasLichen = hasLichen;
            }
        }

        public readonly struct Result
        {
            public readonly Verdict Verdict;
            /// <summary>Index of the chosen candidate (Ok) or of the nearest taken tree (Taken); -1 for NoTree.</summary>
            public readonly int Index;

            public Result(Verdict verdict, int index)
            {
                Verdict = verdict;
                Index = index;
            }
        }

        /// <summary>
        /// The nearest eligible tree within the radius that has no lichen yet; if every eligible tree in range already carries
        /// lichen, Taken (planting would only reset it); otherwise NoTree.
        /// </summary>
        public static Result Choose(IReadOnlyList<Candidate> candidates, ICollection<int> eligibleHashes)
        {
            var free = -1;
            var taken = -1;
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!eligibleHashes.Contains(c.PrefabHash) || float.IsNaN(c.Distance) || c.Distance > TrunkRadius) continue;
                if (c.HasLichen)
                {
                    if (taken < 0 || c.Distance < candidates[taken].Distance) taken = i;
                }
                else if (free < 0 || c.Distance < candidates[free].Distance) free = i;
            }
            if (free >= 0) return new Result(Verdict.Ok, free);
            return taken >= 0 ? new Result(Verdict.Taken, taken) : new Result(Verdict.NoTree, -1);
        }
    }
}
