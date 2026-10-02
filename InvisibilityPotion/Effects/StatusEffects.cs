using Jotunn.Entities;
using Jotunn.Managers;
using InvisibilityPotion.Config;
using UnityEngine;

namespace InvisibilityPotion.Effects
{
    public static class StatusEffects
    {
        public const string RevealedName = "SE_IP_Revealed";
        public static readonly int RevealedHash = RevealedName.GetStableHashCode();

        public static string EffectName(int tier) => $"SE_Invisibility_T{tier}";
        public static int NameHash(int tier) => EffectName(tier).GetStableHashCode();

        // Jotunn adds these exact instances to every ObjectDB (ItemManager.RegisterCustomStatusEffects: objectDB.m_StatusEffects.Add(customStatusEffect.StatusEffect)),
        // but only on ObjectDB.Awake, which runs after OnVanillaPrefabsAvailable. Item registration must use these references, not an ObjectDB lookup.
        private static readonly SE_Invisibility[] _prefabs = new SE_Invisibility[4];

        /// <summary>The registered SE_Invisibility prefab of a tier (1..3), or null before Register or for an invalid tier.</summary>
        public static SE_Invisibility Prefab(int tier) => tier >= 1 && tier <= 3 ? _prefabs[tier] : null;

        /// <summary>The registered SE_Revealed prefab, or null before Register.</summary>
        public static SE_Revealed RevealedPrefab { get; private set; }

        public static void Register()
        {
            for (var t = 1; t <= 3; t++)
            {
                var se = ScriptableObject.CreateInstance<SE_Invisibility>();
                se.name = EffectName(t);
                se.Tier = t;
                se.m_name = $"$ip_se_name_t{t}";
                se.m_tooltip = $"$ip_se_tooltip_t{t}";
                se.m_icon = Items.Icons.Veil(t);  // embedded se_veil_t{t}.png; the HUD hides status effects without an icon
                se.m_startMessage = "$ip_se_start";
                se.m_stopMessage = "$ip_se_stop";
                se.m_startMessageType = MessageHud.MessageType.Center;
                se.m_stopMessageType = MessageHud.MessageType.Center;
                // no m_category: a shared category makes vanilla refuse tier upgrades (decompile-notes §Consume)
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
                _prefabs[t] = se;
            }
            var rev = ScriptableObject.CreateInstance<SE_Revealed>();
            rev.name = RevealedName;
            rev.m_name = "$ip_se_revealed_name";   // "Veil Broken"
            rev.m_tooltip = "$ip_se_revealed_tooltip";
            rev.m_icon = Items.Icons.Sprite("se_veil_broken");   // round Q: shows in the status bar (SEMan.GetHUDStatusEffects needs m_icon)
            rev.m_ttl = GameplayDefaults.DebuffDuration;   // the clone takes the revealing tier's DebuffDuration in Setup; the HUD shows m_ttl - m_time
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(rev, fixReference: false));
            RevealedPrefab = rev;
        }
    }
}
