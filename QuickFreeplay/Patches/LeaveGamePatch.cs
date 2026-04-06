using HarmonyLib;

namespace QuickFreeplay
{
    /// <summary>
    /// Resets mod state when the user navigates away from the current game session
    /// (to main menu or treehouse) while Quick Freeplay is active.
    ///
    /// Without this, State stays IN_FREEPLAY after the scene tears down, and the
    /// next toggle press calls TryReturnToMatch → ServerChangeScene with no active
    /// server, crashing the game.
    /// </summary>

    [HarmonyPatch(typeof(GameControl), "BackToMainMenu")]
    public static class BackToMainMenuPatch
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            var mgr = QuickFreeplayManager.Instance;
            if (mgr != null && mgr.State != QuickFreeplayManager.ModState.IDLE)
            {
                mgr.ResetState();
                QuickFreeplayPlugin.Log.LogInfo("[QFP] State reset — BackToMainMenu called.");
            }
        }
    }

    [HarmonyPatch(typeof(GameControl), "fadeToLobby")]
    public static class FadeToLobbyPatch
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            var mgr = QuickFreeplayManager.Instance;
            if (mgr != null && mgr.State != QuickFreeplayManager.ModState.IDLE)
            {
                mgr.ResetState();
                QuickFreeplayPlugin.Log.LogInfo("[QFP] State reset — fadeToLobby called.");
            }
        }
    }
}
