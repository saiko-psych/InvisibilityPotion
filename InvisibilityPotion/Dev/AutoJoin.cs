#if DEBUG
using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace InvisibilityPotion.Dev
{
    /// <summary>
    /// Debug-only: skips the main menu and loads a local world. Configuration comes from
    /// BepInEx/config/InvisibilityPotion.autojoin (world=..., character=...), with the env vars
    /// IP_DEV_WORLD / IP_DEV_CHARACTER as a fallback. Empty world means: do nothing.
    /// Mirrors FejdStartup.OnCharacterStart + OnWorldStart (see task-5 report for line references).
    /// </summary>
    [HarmonyPatch(typeof(FejdStartup), "Start")]
    internal static class AutoJoin
    {
        private const string FileName = "InvisibilityPotion.autojoin";
        // Set before the attempt on purpose: auto-join fires at most once per process. Returning to the
        // menu does not re-join, and a failed join is not retried.
        private static bool _done;

        [HarmonyPostfix]
        private static void Postfix(FejdStartup __instance)
        {
            if (_done) return;
            try
            {
                string content = null;
                var path = Path.Combine(Paths.ConfigPath, FileName);
                if (File.Exists(path))
                {
                    content = File.ReadAllText(path);
                    // One-shot: a normal Steam start must not auto-join. `make run` rewrites the file every time.
                    try
                    {
                        File.Delete(path);
                        Plugin.Log.LogInfo($"AutoJoin: consumed and deleted {FileName}");
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning($"AutoJoin: could not delete {FileName}: {e.Message}");
                    }
                }

                var (world, character) = AutoJoinConfig.Parse(content,
                    Environment.GetEnvironmentVariable("IP_DEV_WORLD"),
                    Environment.GetEnvironmentVariable("IP_DEV_CHARACTER"));
                if (string.IsNullOrEmpty(world)) return;

                _done = true;
                __instance.StartCoroutine(JoinWhenMenuReady(__instance, world, character));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"AutoJoin setup failed: {e}");
            }
        }

        private static IEnumerator JoinWhenMenuReady(FejdStartup startup, string worldName, string characterName)
        {
            // Let FejdStartup finish its own start-up frame, then wait out the intro cinematic (capped).
            yield return null;
            var deadline = Time.realtimeSinceStartup + 30f;
            while (CinematicsManager.IsStartedPlaying() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (CinematicsManager.IsStartedPlaying())
            {
                Plugin.Log.LogWarning("AutoJoin: intro cinematic still playing after 30 s; joining anyway");
            }
            yield return null;
            Join(startup, worldName, characterName);
        }

        private static void Join(FejdStartup startup, string worldName, string characterName)
        {
            try
            {
                var profiles = startup.m_profiles ?? SaveSystem.GetAllPlayerProfiles();
                startup.m_profiles = profiles;
                if (profiles.Count == 0)
                {
                    Plugin.Log.LogError("AutoJoin: no characters exist. Create one manually once; staying in the menu.");
                    return;
                }

                int index = startup.m_profileIndex;
                if (!string.IsNullOrEmpty(characterName))
                {
                    index = profiles.FindIndex(p =>
                        string.Equals(p.GetName(), characterName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.GetFilename(), characterName, StringComparison.OrdinalIgnoreCase));
                    if (index < 0)
                    {
                        Plugin.Log.LogError($"AutoJoin: character '{characterName}' not found. Available: {string.Join(", ", profiles.Select(p => p.GetName()))}");
                        return;
                    }
                }
                else if (index < 0 || index >= profiles.Count)
                {
                    index = 0;
                }
                var profile = profiles[index];

                var worlds = SaveSystem.GetWorldList();
                var world = worlds.FirstOrDefault(w => string.Equals(w.m_name, worldName, StringComparison.OrdinalIgnoreCase));
                if (world == null)
                {
                    Plugin.Log.LogError($"AutoJoin: world '{worldName}' not found. Available: {string.Join(", ", worlds.Select(w => w.m_name))}");
                    return;
                }
                if (world.m_dataError != World.SaveDataError.None)
                {
                    Plugin.Log.LogError($"AutoJoin: world '{world.m_name}' has save data error {world.m_dataError}; fix it in the menu first.");
                    return;
                }

                Plugin.Log.LogInfo($"AutoJoin: loading world '{world.m_name}' as '{profile.GetName()}'");

                // Mirror FejdStartup.OnCharacterStart: SelectCharacter(...) = PlatformPrefs "profile" + Game.SetProfile.
                startup.m_profileIndex = index;
                PlatformPrefs.SetString("profile", profile.GetFilename());
                Game.SetProfile(profile.GetFilename(), profile.m_fileSource);

                // Mirror FejdStartup.OnWorldStart (SaveDataError.None branch) for a local, non-open, non-public, non-crossplay world.
                Game.m_serverOptionsSummary = "";
                PlatformPrefs.SetString("world", world.m_name);
                ZNet.m_onlineBackend = OnlineBackendType.Steamworks;
                ZSteamMatchmaking.instance.StopServerListing();
                startup.m_startingWorld = true;
                ZNet.SetServer(server: true, openServer: false, publicServer: false, world.m_name, "", world);
                ZNet.ResetServerHost();
                startup.TransitionToMainScene();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"AutoJoin failed: {e}");
            }
        }
    }
}
#endif
