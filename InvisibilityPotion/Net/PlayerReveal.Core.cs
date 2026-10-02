using InvisibilityPotion.Goggles;

namespace InvisibilityPotion.Net
{
    /// <summary>Server-side decision for the per-peer header position of a player ZDO (spec plan 5 §3.5 part 3), no game types.</summary>
    public static class PlayerReveal
    {
        /// <summary>
        /// True when the receiving peer must get the spoofed position: the ZDO is a player hidden from players, the peer does not own
        /// it, and the peer's own player ZDO does not claim level III goggles (or the server switch is off).
        /// </summary>
        public static bool ShouldSpoof(bool isHiddenPlayer, bool peerIsOwner, int peerGoggles, bool revealSwitch) =>
            isHiddenPlayer && !peerIsOwner && !GoggleLevel.SeesHiddenPlayers(peerGoggles, revealSwitch);
    }
}
