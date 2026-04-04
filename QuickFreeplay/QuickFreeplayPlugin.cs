using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace QuickFreeplay
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public class QuickFreeplayPlugin : BaseUnityPlugin
    {
        public static QuickFreeplayPlugin Instance { get; private set; }
        public static ManualLogSource Log          { get; private set; }

        void Awake()
        {
            Instance = this;
            Log      = Logger;

            QuickFreeplayConfig.Init(Config);

            if (!ReflectionCache.Validate())
            {
                Log.LogError("[QFP] One or more reflection lookups failed — " +
                             "the mod may not work correctly. Check for a game update.");
            }

            new Harmony(PluginInfo.GUID).PatchAll();

            var go = new GameObject("QuickFreeplayManager");
            DontDestroyOnLoad(go);
            go.AddComponent<QuickFreeplayManager>();
            go.AddComponent<QuickFreeplayOverlay>();

            Log.LogInfo($"[QFP] {PluginInfo.NAME} v{PluginInfo.VERSION} loaded. " +
                        $"Toggle: {QuickFreeplayConfig.HotkeyToggle}");
        }
    }

    internal static class PluginInfo
    {
        public const string GUID    = "com.ossie.quickfreeplay";
        public const string NAME    = "QuickFreeplay";
        public const string VERSION = "1.0.0";
    }
}
