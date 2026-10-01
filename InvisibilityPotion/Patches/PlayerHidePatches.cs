using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using InvisibilityPotion.Net;
using UnityEngine;

namespace InvisibilityPotion.Patches
{
    /// <summary>Tier III: other players see neither nameplate (viewer side) nor map pin nor real position (server side). decompile-notes §Network, §Visuals.</summary>
    [HarmonyPatch]
    internal static class PlayerHidePatches
    {
        /// <summary>Height of the spoofed header position: far above the world, same x/z so the sector column is unchanged.</summary>
        public const float SpoofHeight = 10000f;

        /// <summary>Viewer side: no nameplate for a hidden player. EnemyHud.TestShow(Character c, bool isVisible) EnemyHud.cs:101.</summary>
        [HarmonyPatch(typeof(EnemyHud), "TestShow")]
        [HarmonyPostfix]
        private static void TestShow_Postfix(Character c, ref bool __result)
        {
            if (!__result || c == null || !c.IsPlayer() || c == Player.m_localPlayer) return;
            if (HiddenState.IsHiddenFromPlayers(c)) __result = false;
        }

        /// <summary>Server side (only SendPlayerList calls it, behind IsServer): clear the map position of hidden players. Covers peers and the listen host's own entry. ZNet.cs:2454.</summary>
        [HarmonyPatch(typeof(ZNet), "UpdatePlayerList")]
        [HarmonyPostfix]
        private static void UpdatePlayerList_Postfix(ZNet __instance)
        {
            var zdoMan = ZDOMan.instance;
            if (zdoMan == null) return;
            var players = __instance.m_players;
            for (var i = 0; i < players.Count; i++)
            {
                var info = players[i];
                if (info.m_characterID.IsNone()) continue;
                if (!HiddenState.IsHiddenFromPlayers(zdoMan.GetZDO(info.m_characterID))) continue;
                info.m_publicPosition = false;
                info.m_position = Vector3.zero;
                players[i] = info; // PlayerInfo is a struct: write the copy back
            }
        }

        // ZDOPeer is a private nested class (ZDOMan.cs:10). The publicized assembly exposes it at compile time, but the
        // helper takes it as object and reads m_peer (ZDOMan.cs:28) through a field ref, so the private type is never named.
        // Built lazily on the first hidden-player send so a failure cannot break the other patches in this class.
        private static AccessTools.FieldRef<object, ZNetPeer> _peerField;
        private static bool _peerFieldFailed;

        private static ZNetPeer PeerOf(object zdoPeer)
        {
            if (_peerField == null)
            {
                if (_peerFieldFailed) return null;
                try
                {
                    _peerField = AccessTools.FieldRefAccess<object, ZNetPeer>(AccessTools.Field(AccessTools.Inner(typeof(ZDOMan), "ZDOPeer"), "m_peer"));
                }
                catch (Exception e)
                {
                    _peerFieldFailed = true;
                    Plugin.Log.LogError($"ZDOPeer.m_peer not accessible, position spoof disabled: {e.Message}");
                    return null;
                }
            }
            return _peerField(zdoPeer);
        }

        private static readonly MethodInfo GetPositionMethod = AccessTools.Method(typeof(ZDO), nameof(ZDO.GetPosition));
        private static readonly MethodInfo WriteVector3Method = AccessTools.Method(typeof(ZPackage), nameof(ZPackage.Write), new[] { typeof(Vector3) });
        private static readonly MethodInfo HeaderPositionMethod = AccessTools.Method(typeof(PlayerHidePatches), nameof(HeaderPosition));

        /// <summary>
        /// ZDOMan.SendZDOs(ZDOPeer peer, bool flush), ZDOMan.cs:1074. The per-ZDO header writes the position (ZDOMan.cs:1126):
        ///   ldloc.2; ldloc.s item2; callvirt ZDO::GetPosition(); callvirt ZPackage::Write(Vector3)
        /// The single GetPosition call that is immediately followed by Write(Vector3) is replaced by
        ///   ldarg.1; call HeaderPosition(ZDO, object)
        /// so the ZDO already on the stack (GetPosition's receiver) and the peer become the helper's arguments and a Vector3
        /// is left for Write, exactly as before. Anything other than exactly one match leaves the IL untouched.
        /// </summary>
        [HarmonyPatch(typeof(ZDOMan), "SendZDOs")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SendZDOs_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            var codes = new List<CodeInstruction>(original.Count + 1);
            var replaced = 0;
            for (var i = 0; i < original.Count; i++)
            {
                var ins = original[i];
                if (ins.Calls(GetPositionMethod) && i + 1 < original.Count && original[i + 1].Calls(WriteVector3Method))
                {
                    var ldPeer = new CodeInstruction(OpCodes.Ldarg_1);
                    ldPeer.labels.AddRange(ins.labels);
                    ldPeer.blocks.AddRange(ins.blocks);
                    codes.Add(ldPeer);
                    codes.Add(new CodeInstruction(OpCodes.Call, HeaderPositionMethod));
                    replaced++;
                    continue;
                }
                codes.Add(ins);
            }
            if (replaced != 1)
            {
                Plugin.Log.LogError("SendZDOs transpiler: pattern not found, position spoof disabled");
                return original;
            }
            return codes;
        }

        /// <summary>Called from the patched SendZDOs for every ZDO header. Server only: a client's peer is the server, which must get the real position.</summary>
        public static Vector3 HeaderPosition(ZDO zdo, object zdoPeer)
        {
            var real = zdo.GetPosition();
            if (zdoPeer == null || !HiddenState.IsHiddenFromPlayers(zdo)) return real;
            var znet = ZNet.instance;
            if (znet == null || !znet.IsServer()) return real;
            var peer = PeerOf(zdoPeer);
            if (peer == null || zdo.GetOwner() == peer.m_uid) return real; // the owner always gets its own real position
            return new Vector3(real.x, SpoofHeight, real.z);
        }
    }
}
