using System;
using BepInEx;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using DiscordRPC;
using HarmonyLib;
using Steamworks;
using ZortLibrary;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZortRichPresence
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(ZortLib.libGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private const string DiscordApplicationId = "1558897439505907772";
        private const uint SteamAppId = 3121110;

        public static ManualLogSource ModLogger;

        private ConfigEntry<bool> _presenceEnabled;
        private ConfigEntry<bool> _showSteamProfile;
        private ConfigEntry<bool> _showDifficulty;
        private ConfigEntry<bool> _showJoinLobby;
        private object _discordClientInstance;
        private Harmony _harmony;
        private string _presenceDetails;
        private string _presenceState;
        private bool _inGame;
        private string _activeSceneName;

        private void Awake()
        {
            ModLogger = base.Logger;
            ModLogger.LogInfo($"Starting mod {MyPluginInfo.PLUGIN_GUID}...");

            try
            {
                _presenceEnabled = Config.Bind(
                    "Discord Rich Presence",
                    "Enabled",
                    true,
                    "Enables or disables Discord Rich Presence.");

                _showSteamProfile = Config.Bind(
                    "Discord Rich Presence",
                    "ShowSteamProfile",
                    false,
                    "Shows a Steam profile button in Discord Rich Presence.");

                _showDifficulty = Config.Bind(
                    "Discord Rich Presence",
                    "ShowDifficulty",
                    false,
                    "Shows the detected difficulty in Discord Rich Presence.");

                _showJoinLobby = Config.Bind(
                    "Discord Rich Presence",
                    "ShowJoinLobby",
                    false,
                    "Allows others to join the current Steam lobby through Discord.");

                SettingsLib.Initialize(
                    this,
                    _presenceEnabled,
                    OnPresenceSettingChanged,
                    _showSteamProfile,
                    OnSteamProfileSettingChanged,
                    _showDifficulty,
                    OnDifficultySettingChanged,
                    _showJoinLobby,
                    OnJoinLobbySettingChanged);
                RegisterZortEvents();
                if (_presenceEnabled.Value)
                    InitializeDiscordPresence();

                _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
                _harmony.PatchAll();

                ModLogger.LogInfo("Harmony patches loaded.");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"Error while starting the mod: {ex}");
            }
        }

        private void InitializeDiscordPresence()
        {
            try
            {
                var client = new DiscordRpcClient(DiscordApplicationId);
                client.Initialize();
                _discordClientInstance = client;
                ClearPresence();
                ModLogger.LogInfo("Discord Rich Presence started.");
            }
            catch (TypeLoadException ex)
            {
                ModLogger.LogError($"DiscordRPC.dll could not be loaded. Check the plugins folder. Details: {ex.Message}");
            }
            catch (Exception ex)
            {
                ModLogger.LogError($"General error while starting Discord RPC: {ex}");
            }
        }

        private void OnPresenceSettingChanged(bool enabled)
        {
            if (enabled)
            {
                if (_discordClientInstance == null)
                    InitializeDiscordPresence();
                else
                    ClearPresence();
            }
            else if (_discordClientInstance is DiscordRpcClient client)
            {
                client.ClearPresence();
                client.Dispose();
                _discordClientInstance = null;
                ModLogger.LogInfo("Discord Rich Presence disabled from settings.");
            }
        }

        private void OnSteamProfileSettingChanged(bool enabled)
        {
            UpdatePresence();
        }

        private void OnDifficultySettingChanged(bool enabled)
        {
            UpdatePresence();
        }

        private void OnJoinLobbySettingChanged(bool enabled)
        {
            UpdatePresence();
        }

        private void RegisterZortEvents()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ZortLib.OnceMenu += OnMenu;
            ZortLib.OnceLobby += OnLobby;
            ZortLib.OnceGame += OnGame;
            PlayerController.OnPlayerDied += OnPlayerDied;
            PlayerController.OnPlayerRespawned += OnPlayerRespawned;
        }

        private void UnregisterZortEvents()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ZortLib.OnceMenu -= OnMenu;
            ZortLib.OnceLobby -= OnLobby;
            ZortLib.OnceGame -= OnGame;
            PlayerController.OnPlayerDied -= OnPlayerDied;
            PlayerController.OnPlayerRespawned -= OnPlayerRespawned;
        }

        private void OnMenu()
        {
            _inGame = false;
            _activeSceneName = null;
            _presenceDetails = null;
            _presenceState = null;
            ClearPresence();
        }

        private void OnLobby()
        {
            _inGame = false;
            SetPresence("In the Lobby", $"{GetLobbyPlayerCount()} Players in Lobby");
        }

        private void OnGame()
        {
            _inGame = true;
            SetPresence(GetMapDisplayName(), GetGameStateText());
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            _activeSceneName = currentScene.name;

            if (IsMainMenuScene(currentScene.name))
            {
                OnMenu();
            }
            else if (IsMapScene(currentScene.name))
            {
                OnGame();
            }
            else if (currentScene.name == "InGame")
            {
                OnLobby();
            }
        }

        private void OnPlayerDied(PlayerController player)
        {
            if (_inGame)
                SetPresence(GetMapDisplayName(), GetGameStateText());
        }

        private void OnPlayerRespawned(PlayerController player, bool wasRevived)
        {
            if (_inGame)
                SetPresence(GetMapDisplayName(), GetGameStateText());
        }

        private string GetGameStateText()
        {
            string aliveText = $"{GetAlivePlayerCount()} Players Alive";
            if (_showDifficulty == null || !_showDifficulty.Value)
                return aliveText;

            string difficulty = DifficultyScanner.TryGetDifficulty();
            return string.IsNullOrEmpty(difficulty)
                ? aliveText
                : $"{difficulty} • {aliveText}";
        }

        private string GetMapDisplayName()
        {
            string sceneName = _activeSceneName ?? string.Empty;
            if (sceneName.IndexOf("yozgat", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Yozgat";
            if (sceneName.IndexOf("seagate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Seagate";
            if (sceneName.IndexOf("zortagane", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Zortagane";

            return sceneName;
        }

        private static int GetLobbyPlayerCount()
        {
            if (SteamManager.CurrentLobby.HasValue)
                return SteamMatchmaking.GetNumLobbyMembers(SteamManager.CurrentLobby.Value);

            return PlayerController.AllPlayers == null ? 0 : PlayerController.AllPlayers.Count;
        }

        private static int GetAlivePlayerCount()
        {
            if (PlayerController.AllPlayers == null)
                return 0;

            int alivePlayers = 0;
            foreach (PlayerController player in PlayerController.AllPlayers)
            {
                if (player != null && !player.IsDead)
                    alivePlayers++;
            }

            return alivePlayers;
        }

        private void SetPresence(string details, string state)
        {
            _presenceDetails = details;
            _presenceState = state;

            if (_presenceEnabled != null && !_presenceEnabled.Value)
                return;

            if (!(_discordClientInstance is DiscordRpcClient client))
                return;

            client.SetPresence(new RichPresence
            {
                Details = details,
                State = state,
                Timestamps = Timestamps.Now,
                Assets = new Assets
                {
                    LargeImageKey = GetPresenceImageKey(),
                    LargeImageText = "Zort"
                },
                Buttons = GetPresenceButtons()
            });
        }

        private string GetPresenceImageKey()
        {
            if (!_inGame)
                return "lobbyfoto";

            string sceneName = _activeSceneName ?? string.Empty;
            if (sceneName.IndexOf("yozgat", StringComparison.OrdinalIgnoreCase) >= 0)
                return "yozgatfoto";
            if (sceneName.IndexOf("seagate", StringComparison.OrdinalIgnoreCase) >= 0)
                return "seagatefoto";
            if (sceneName.IndexOf("zortagane", StringComparison.OrdinalIgnoreCase) >= 0)
                return "zortaganefoto";

            return "zortaganefoto";
        }

        private static bool IsMainMenuScene(string sceneName)
        {
            return sceneName == "MainMenu" || sceneName == "Init";
        }

        private static bool IsMapScene(string sceneName)
        {
            return sceneName.IndexOf("zortagane", StringComparison.OrdinalIgnoreCase) >= 0
                || sceneName.IndexOf("seagate", StringComparison.OrdinalIgnoreCase) >= 0
                || sceneName.IndexOf("yozgat", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ClearPresence()
        {
            if (_discordClientInstance is DiscordRpcClient client)
                client.ClearPresence();
        }

        private void UpdatePresence()
        {
            if (string.IsNullOrEmpty(_presenceDetails) || string.IsNullOrEmpty(_presenceState))
            {
                ClearPresence();
                return;
            }

            SetPresence(_presenceDetails, _presenceState);
        }

        private DiscordRPC.Button[] GetPresenceButtons()
        {
            var buttons = new List<DiscordRPC.Button>();
            try
            {
                if (_showSteamProfile != null && _showSteamProfile.Value)
                {
                    ulong steamId = (ulong)SteamUser.GetSteamID();
                    if (steamId != 0)
                    {
                        buttons.Add(new DiscordRPC.Button
                        {
                            Label = "Show Steam Profile",
                            Url = "https://steamcommunity.com/profiles/" + steamId
                        });
                    }
                }

                if (_showJoinLobby != null && _showJoinLobby.Value && SteamManager.CurrentLobby.HasValue)
                {
                    CSteamID lobby = SteamManager.CurrentLobby.Value;
                    CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby);
                    ulong ownerId = (ulong)owner;

                    ulong localId = (ulong)SteamUser.GetSteamID();
                    if (ownerId != 0 && localId == ownerId)
                    {
                        buttons.Add(new DiscordRPC.Button
                        {
                            Label = "Join Lobby",
                            Url = $"steam://joinlobby/{SteamAppId}/{(ulong)lobby}/{ownerId}"
                        });
                    }
                }

                return buttons.Count == 0 ? null : buttons.ToArray();
            }
            catch (Exception ex)
            {
                ModLogger.LogWarning($"Could not create the Steam profile button: {ex.Message}");
                return null;
            }
        }

        private void OnDestroy()
        {
            SettingsLib.Shutdown();
            UnregisterZortEvents();

            if (_discordClientInstance is DiscordRpcClient client)
            {
                try
                {
                    client.ClearPresence();
                    client.Dispose();
                    ModLogger.LogInfo("Discord Rich Presence stopped.");
                }
                catch (Exception ex)
                {
                    ModLogger.LogError($"Error while stopping Discord: {ex.Message}");
                }
            }
            _discordClientInstance = null;

            _harmony?.UnpatchSelf();
            _harmony = null;
        }
    }

    [HarmonyPatch(typeof(InitUI), nameof(InitUI.ApplyChanges_Btn))]
    internal static class ApplyPresenceSettingPatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            SettingsLib.ApplyPending();
        }
    }

    [HarmonyPatch(typeof(ButtonFunctions), nameof(ButtonFunctions.OpenSettings_Btn))]
    internal static class OpenSettingsSelectionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ButtonFunctions __instance)
        {
            if (__instance == null)
                return true;

            GameObject canvas = FindInGameCanvas();
            Transform settingsPanel = canvas == null
                ? null
                : canvas.transform.Find("settingsPanel");

            if (settingsPanel == null)
                return true;

            GameObject loading = canvas.transform.Find("loading...")?.gameObject;
            if (loading != null)
                loading.SetActive(false);

            settingsPanel.gameObject.SetActive(true);

            Selectable firstSelectable = settingsPanel.GetComponentInChildren<Selectable>(true);
            if (firstSelectable != null)
            {
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);

                firstSelectable.Select();
            }

            return false;
        }

        private static GameObject FindInGameCanvas()
        {
            GameObject canvas = GameObject.Find("InGameUICanvas");
            if (canvas != null)
                return canvas;

            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == "InGameUICanvas" && candidate.scene.IsValid())
                    return candidate;
            }

            return null;
        }
    }
}
