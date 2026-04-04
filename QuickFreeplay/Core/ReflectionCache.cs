using System.Reflection;
using HarmonyLib;

namespace QuickFreeplay
{
    /// <summary>
    /// Pre-cached reflection handles for protected/private game fields.
    /// All lookups happen once at plugin load time.
    /// </summary>
    public static class ReflectionCache
    {
        // GameControl.roundNumber  (protected int)
        public static readonly FieldInfo RoundNumber =
            AccessTools.Field(typeof(GameControl), "roundNumber");

        // GameControl.placedBlocks  (protected List<Placeable>)
        public static readonly FieldInfo PlacedBlocks =
            AccessTools.Field(typeof(GameControl), "placedBlocks");

        // GameControl.nextPhase  (protected GamePhase)
        public static readonly FieldInfo NextPhase =
            AccessTools.Field(typeof(GameControl), "nextPhase");

        // Placeable.placed  (protected bool)
        public static readonly FieldInfo PlaceablePlaced =
            AccessTools.Field(typeof(Placeable), "placed");

        // VersusControl.lastRoundsMode  (protected bool)
        // Tracks whether the match has entered "last rounds" mode (end-of-match countdown).
        public static readonly FieldInfo LastRoundsMode =
            AccessTools.Field(typeof(VersusControl), "lastRoundsMode");

        // VersusControl.lastRoundsToGo  (protected int, default 3)
        // Decrements each round in lastRoundsMode; when <= 1, score threshold drops to 0
        // forcing a winner and ending the match.
        public static readonly FieldInfo LastRoundsToGo =
            AccessTools.Field(typeof(VersusControl), "lastRoundsToGo");

        // ScoreKeeper.historyPointBlocks  (private List<PointBlock>)
        // All tallied PointBlocks from previous rounds — needed for capture/save.
        public static readonly FieldInfo HistoryPointBlocks =
            AccessTools.Field(typeof(ScoreKeeper), "historyPointBlocks");

        // GameControl.CurrentPlayerQueue  (public Queue<GamePlayer>)
        // Accessed via reflection to avoid net472/mscorlib Queue<> type-forwarding conflict.
        public static readonly System.Reflection.PropertyInfo CurrentPlayerQueue =
            AccessTools.Property(typeof(GameControl), "CurrentPlayerQueue");

        // QuickSaver.GetCurrentXmlSnapshot(bool omitModifiers) → XmlDocument
        // Serialises the entire live scene: base blocks + snapshot blocks + player-placed blocks.
        public static readonly System.Reflection.MethodInfo GetCurrentXmlSnapshot =
            AccessTools.Method(AccessTools.TypeByName("QuickSaver"), "GetCurrentXmlSnapshot");

        // VersusControl.showPartyBox()  (private void)
        // Opens the party box UI during PLACE phase. Called via reflection after scoreboard animation.
        public static readonly System.Reflection.MethodInfo ShowPartyBox =
            AccessTools.Method(typeof(VersusControl), "showPartyBox");

        public static bool Validate()
        {
            bool ok = true;
            if (RoundNumber == null)          { Log("RoundNumber"); ok = false; }
            if (PlacedBlocks == null)         { Log("PlacedBlocks"); ok = false; }
            if (NextPhase == null)            { Log("NextPhase"); ok = false; }
            if (PlaceablePlaced == null)      { Log("PlaceablePlaced"); ok = false; }
            if (LastRoundsMode == null)       { Log("LastRoundsMode"); ok = false; }
            if (LastRoundsToGo == null)       { Log("LastRoundsToGo"); ok = false; }
            if (HistoryPointBlocks == null)   { Log("HistoryPointBlocks"); ok = false; }
            if (CurrentPlayerQueue == null)   { Log("CurrentPlayerQueue"); ok = false; }
            if (GetCurrentXmlSnapshot == null){ Log("GetCurrentXmlSnapshot"); ok = false; }
            if (ShowPartyBox == null)        { Log("ShowPartyBox"); ok = false; }
            return ok;
        }

        private static void Log(string name)
        {
            BepInEx.Logging.Logger.CreateLogSource("QFP")
                .LogError($"[QFP] ReflectionCache: '{name}' lookup failed — game may have updated.");
        }
    }
}
