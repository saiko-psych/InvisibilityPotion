using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace InvisibilityPotion.Effects
{
    public static class StatusEffects
    {
        public const string RevealedName = "SE_IP_Revealed";
        public static readonly int RevealedHash = RevealedName.GetStableHashCode();

        public static string EffectName(int tier) => $"SE_Invisibility_T{tier}";
        public static int NameHash(int tier) => EffectName(tier).GetStableHashCode();

        public static void Register()
        {
            for (var t = 1; t <= 3; t++)
            {
                var se = ScriptableObject.CreateInstance<SE_Invisibility>();
                se.name = EffectName(t);
                se.Tier = t;
                se.m_name = $"$ip_se_name_t{t}";
                se.m_tooltip = $"$ip_se_tooltip_t{t}";
                se.m_startMessage = "$ip_se_start";
                se.m_stopMessage = "$ip_se_stop";
                se.m_startMessageType = MessageHud.MessageType.Center;
                se.m_stopMessageType = MessageHud.MessageType.Center;
                // no m_category: a shared category makes vanilla refuse tier upgrades (decompile-notes §Consume)
                ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, fixReference: false));
            }
            var rev = ScriptableObject.CreateInstance<SE_Revealed>();
            rev.name = RevealedName;
            rev.m_name = "$ip_se_revealed_name";
            rev.m_tooltip = "$ip_se_revealed_tooltip";
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(rev, fixReference: false));
        }
    }
}
