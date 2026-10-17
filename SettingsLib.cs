using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZortRichPresence
{
    internal static class SettingsLib
    {
        private const string ContentPath = "settingsPanel/Panel/Scroll View/Viewport/Content";
        private const string PresenceObjectName = "ZortRichPresenceSetting";
        private const string SteamObjectName = "ZortSteamProfileSetting";
        private const string DifficultyObjectName = "ZortDifficultySetting";
        private const string JoinLobbyObjectName = "ZortJoinLobbySetting";

        private static BaseUnityPlugin _host;
        private static ConfigEntry<bool> _presenceValue;
        private static ConfigEntry<bool> _steamValue;
        private static ConfigEntry<bool> _difficultyValue;
        private static ConfigEntry<bool> _joinLobbyValue;
        private static Action<bool> _onPresenceChanged;
        private static Action<bool> _onSteamChanged;
        private static Action<bool> _onDifficultyChanged;
        private static Action<bool> _onJoinLobbyChanged;
        private static bool _pendingPresence;
        private static bool _pendingSteam;
        private static bool _pendingDifficulty;
        private static bool _pendingJoinLobby;
        private static readonly GameObject[] _createdObjects = new GameObject[8];

        public static void Initialize(
            BaseUnityPlugin host,
            ConfigEntry<bool> presenceValue,
            Action<bool> onPresenceChanged,
            ConfigEntry<bool> steamValue,
            Action<bool> onSteamChanged,
            ConfigEntry<bool> difficultyValue,
            Action<bool> onDifficultyChanged,
            ConfigEntry<bool> joinLobbyValue,
            Action<bool> onJoinLobbyChanged)
        {
            _host = host;
            _presenceValue = presenceValue;
            _steamValue = steamValue;
            _difficultyValue = difficultyValue;
            _joinLobbyValue = joinLobbyValue;
            _onPresenceChanged = onPresenceChanged;
            _onSteamChanged = onSteamChanged;
            _onDifficultyChanged = onDifficultyChanged;
            _onJoinLobbyChanged = onJoinLobbyChanged;
            _pendingPresence = presenceValue.Value;
            _pendingSteam = steamValue.Value;
            _pendingDifficulty = difficultyValue.Value;
            _pendingJoinLobby = joinLobbyValue.Value;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _host.StartCoroutine(InstallWhenReady());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _host.StartCoroutine(InstallWhenReady());
        }

        private static IEnumerator InstallWhenReady()
        {
            for (int i = 0; i < 120; i++)
            {
                if (TryInstall())
                    yield break;
                yield return null;
            }

            Plugin.ModLogger.LogWarning("Settings panel could not be found: " + ContentPath);
        }

        private static bool TryInstall()
        {
            GameObject canvas = FindCanvas();
            if (canvas == null)
                return false;

            Transform content = canvas.transform.Find(ContentPath);
            if (content == null)
                return false;

            if (content.Find(PresenceObjectName) == null)
            {
                CreateLine(content, "ZortRichPresenceLine");
                CreateToggle(content, PresenceObjectName, "Enable Discord Rich Presence", _pendingPresence, enabled =>
                {
                    _pendingPresence = enabled;
                    SetApplyButtonInteractable(true);
                });
            }

            if (content.Find(SteamObjectName) == null)
            {
                CreateToggle(content, SteamObjectName, "Show Steam Profile", _pendingSteam, enabled =>
                {
                    _pendingSteam = enabled;
                    SetApplyButtonInteractable(true);
                });
            }

            if (content.Find(DifficultyObjectName) == null)
            {
                CreateToggle(content, DifficultyObjectName, "Show Difficulty", _pendingDifficulty, enabled =>
                {
                    _pendingDifficulty = enabled;
                    SetApplyButtonInteractable(true);
                });
            }

            if (content.Find(JoinLobbyObjectName) == null)
            {
                CreateToggle(content, JoinLobbyObjectName, "Allow Lobby Joining (Experimental)", _pendingJoinLobby, enabled =>
                {
                    _pendingJoinLobby = enabled;
                    SetApplyButtonInteractable(true);
                });
            }

            return true;
        }

        private static void CreateLine(Transform content, string objectName)
        {
            if (content.Find(objectName) != null)
                return;

            Transform template = content.Find("Line");
            if (template == null)
                return;

            GameObject line = UnityEngine.Object.Instantiate(template.gameObject, content, false);
            line.name = objectName;
            TrackObject(line);
        }

        private static void CreateToggle(
            Transform content,
            string objectName,
            string labelText,
            bool initialValue,
            Action<bool> onChanged)
        {
            Transform template = content.Find("subtitlesToggle");
            if (template == null)
            {
                Plugin.ModLogger.LogWarning("The subtitlesToggle template could not be found.");
                return;
            }

            GameObject row = UnityEngine.Object.Instantiate(template.gameObject, content, false);
            row.name = objectName;
            TrackObject(row);

            Toggle toggle = row.GetComponentInChildren<Toggle>(true);
            if (toggle == null)
            {
                UnityEngine.Object.Destroy(row);
                return;
            }

            toggle.onValueChanged.RemoveAllListeners();
            toggle.SetIsOnWithoutNotify(initialValue);
            toggle.onValueChanged.AddListener(new UnityAction<bool>(onChanged));

            SetLabel(row, labelText);
            _host.StartCoroutine(ApplyLabelNextFrame(row, labelText));
        }

        private static void SetLabel(GameObject row, string labelText)
        {
            if (labelText != null && labelText.StartsWith("Steam Profilini ", StringComparison.Ordinal))
                labelText = "Show Steam Profile";
            if (labelText != null && labelText.StartsWith("ZortDifficulty", StringComparison.OrdinalIgnoreCase))
                labelText = "Show Difficulty";

            Transform optionText = row.transform.Find("Text/OptionText");
            TMP_Text label = optionText == null ? null : optionText.GetComponent<TMP_Text>();
            if (label != null)
                label.text = labelText;
        }

        private static IEnumerator ApplyLabelNextFrame(GameObject row, string labelText)
        {
            for (int i = 0; i < 30 && row != null; i++)
            {
                yield return null;
                SetLabel(row, labelText);
            }
        }

        private static void TrackObject(GameObject obj)
        {
            for (int i = 0; i < _createdObjects.Length; i++)
            {
                if (_createdObjects[i] == null)
                {
                    _createdObjects[i] = obj;
                    return;
                }
            }
        }

        private static void SetApplyButtonInteractable(bool interactable)
        {
            GameObject canvas = FindCanvas();
            if (canvas == null)
                return;

            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == "ApplyButton")
                {
                    button.interactable = interactable;
                    return;
                }
            }
        }

        public static void ApplyPending()
        {
            if (_presenceValue != null && _pendingPresence != _presenceValue.Value)
            {
                _presenceValue.Value = _pendingPresence;
                _onPresenceChanged?.Invoke(_pendingPresence);
            }

            if (_steamValue != null && _pendingSteam != _steamValue.Value)
            {
                _steamValue.Value = _pendingSteam;
                _onSteamChanged?.Invoke(_pendingSteam);
            }

            if (_difficultyValue != null && _pendingDifficulty != _difficultyValue.Value)
            {
                _difficultyValue.Value = _pendingDifficulty;
                _onDifficultyChanged?.Invoke(_pendingDifficulty);
            }

            if (_joinLobbyValue != null && _pendingJoinLobby != _joinLobbyValue.Value)
            {
                _joinLobbyValue.Value = _pendingJoinLobby;
                _onJoinLobbyChanged?.Invoke(_pendingJoinLobby);
            }
        }

        private static GameObject FindCanvas()
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

        public static void Shutdown()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            for (int i = 0; i < _createdObjects.Length; i++)
            {
                if (_createdObjects[i] != null)
                    UnityEngine.Object.Destroy(_createdObjects[i]);
                _createdObjects[i] = null;
            }

            _host = null;
            _presenceValue = null;
            _steamValue = null;
            _difficultyValue = null;
            _joinLobbyValue = null;
            _onPresenceChanged = null;
            _onSteamChanged = null;
            _onDifficultyChanged = null;
            _onJoinLobbyChanged = null;
        }
    }
}
