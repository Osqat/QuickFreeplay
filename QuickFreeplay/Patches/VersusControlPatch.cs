using System.Reflection;
using HarmonyLib;

namespace QuickFreeplay
{
    /// <summary>
    /// Patches VersusControl.SetupStart.
    ///
    /// Prefix  — before SetupStart body: sets MaxRounds to the REMAINING round count
    ///           so the game (and UCHScoreboard) sees the correct number of rounds.
    ///           roundNumber is NOT forced — it starts at 0 and ToPlaceMode increments
    ///           to 1 naturally, matching the "Round 1 of <remaining>" display.
    ///
    /// Postfix — notifies QuickFreeplayManager that the party controller is fully
    ///           initialised so score restoration can proceed safely.
    /// </summary>
    [HarmonyPatch]
    public static class VersusControlPatch
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(typeof(VersusControl), "SetupStart");

        [HarmonyPrefix]
        static void Prefix(VersusControl __instance, GameState.GameMode mode)
        {
            var mgr = QuickFreeplayManager.Instance;
            if (mgr == null || mgr.State != QuickFreeplayManager.ModState.RESTORING || mgr.SavedSnapshot == null)
                return;

            int remaining = mgr.SavedSnapshot.MaxRounds - mgr.SavedSnapshot.RoundNumber + 1;
            if (remaining < 1) remaining = 1;

            GameSettings.GetInstance().GameMode  = GameState.GameMode.PARTY;
            GameSettings.GetInstance().MaxRounds = remaining;
        }

        [HarmonyPostfix]
        static void Postfix(VersusControl __instance, GameState.GameMode mode)
        {
            QuickFreeplayManager.Instance?.OnVersusControlReady(__instance);
        }
    }

    /// <summary>
    /// Patches VersusControl.showPartyBox (private).
    /// Suppresses the party box from opening when SuppressPartyBox is true,
    /// so the scoreboard animation can play first during score restore.
    /// </summary>
    [HarmonyPatch]
    public static class ShowPartyBoxPatch
    {
        static MethodBase TargetMethod()
            => AccessTools.Method(typeof(VersusControl), "showPartyBox");

        [HarmonyPrefix]
        static bool Prefix()
        {
            var mgr = QuickFreeplayManager.Instance;
            if (mgr != null && mgr.SuppressPartyBox)
                return false; // skip — will be opened after scoreboard finishes
            return true;
        }
    }
}
