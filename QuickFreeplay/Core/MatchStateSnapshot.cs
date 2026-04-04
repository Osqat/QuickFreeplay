using System;
using System.Collections.Generic;

namespace QuickFreeplay
{
    public struct PointBlockData
    {
        public int Type;          // cast of PointBlock.pointBlockType
        public int PlayerNumber;
    }

    public class MatchStateSnapshot
    {
        // Round progress
        public int RoundNumber;
        public int MaxRounds;
        public GameControl.GamePhase PhaseAtCapture;

        // Full PointBlock history (history + pending at capture time).
        // Used to restore the visual scoreboard icons and reconstruct totalScore.
        public List<PointBlockData> PointBlockHistory = new List<PointBlockData>();

        // Level identity
        public string AssociatedScene;

        // Player-placed blocks captured at snapshot time
        public List<PlacedBlockData> PlacedBlocks = new List<PlacedBlockData>();

        // The full level snapshot XML (base level layout — pre-placed traps/platforms).
        // Taken from QuickSaver.lastLoadedXml before the scene reload.
        // Restored into QuickSaver.levelPortalXml before each reload so the game's own
        // OnSetupStartLevel picks it up and applies the level layout automatically.
        public string LevelXml;

        public DateTime CapturedAt;
    }
}
