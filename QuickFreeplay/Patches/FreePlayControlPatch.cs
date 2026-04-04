using System.Reflection;
using HarmonyLib;

namespace QuickFreeplay
{
    /// <summary>
    /// Patches FreePlayControl.SetupStart.
    ///
    /// Prefix  — ensures GameSettings.GameMode is FREEPLAY before the method body
    ///           runs (fixes clients whose local GameSettings still say PARTY).
    ///
    /// Postfix — notifies QuickFreeplayManager that freeplay is ready so the
    ///           overlay can switch to "Return to Match" mode.
    /// </summary>
    [HarmonyPatch]
    public static class FreePlayControlPatch
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(typeof(FreePlayControl), "SetupStart");

        [HarmonyPrefix]
        static void Prefix(FreePlayControl __instance, GameState.GameMode mode)
        {
            var mgr = QuickFreeplayManager.Instance;
            if (mgr != null && mgr.State == QuickFreeplayManager.ModState.IN_FREEPLAY)
                GameSettings.GetInstance().GameMode = GameState.GameMode.FREEPLAY;
        }

        [HarmonyPostfix]
        static void Postfix(FreePlayControl __instance, GameState.GameMode mode)
        {
            QuickFreeplayManager.Instance?.OnFreePlayControlReady(__instance);
        }
    }
}
