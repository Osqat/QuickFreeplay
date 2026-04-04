using BepInEx.Configuration;
using UnityEngine;

namespace QuickFreeplay
{
    public static class QuickFreeplayConfig
    {
        /// <summary>Hotkey that toggles between freeplay and match.</summary>
        public static KeyCode HotkeyToggle => _hotkeyToggle.Value;

        /// <summary>Show a warning when the feature is used during an online game.</summary>
        public static bool WarnInOnlineGame => _warnInOnlineGame.Value;

        private static ConfigEntry<KeyCode> _hotkeyToggle;
        private static ConfigEntry<bool>    _warnInOnlineGame;

        public static void Init(ConfigFile config)
        {
            _hotkeyToggle = config.Bind(
                "Keys", "ToggleHotkey", KeyCode.F3,
                "Press to toggle between the party match and Quick Freeplay.");

            _warnInOnlineGame = config.Bind(
                "Online", "WarnInOnlineGame", true,
                "Show a reminder that Quick Freeplay is host-only in online games.");
        }
    }
}
