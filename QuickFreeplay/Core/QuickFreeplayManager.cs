using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;
using UnityEngine.Networking;

namespace QuickFreeplay
{
    public class QuickFreeplayManager : MonoBehaviour
    {
        public static QuickFreeplayManager Instance { get; private set; }

        public enum ModState { IDLE, IN_FREEPLAY, RESTORING }

        public ModState State { get; private set; } = ModState.IDLE;
        public MatchStateSnapshot SavedSnapshot { get; private set; }

        // When true, the Harmony prefix on showPartyBox() will skip opening it.
        // Set during restore so the scoreboard animation plays before the party box appears.
        public bool SuppressPartyBox { get; set; }


        // Spam guard — minimum seconds between toggle activations.
        private float _lastToggleTime = float.MinValue;
        private const float ToggleCooldown = 3f;

        // Timed on-screen feedback messages
        private string _screenMessage;
        private float _messageExpiry;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            StopAllCoroutines();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(QuickFreeplayConfig.HotkeyToggle))
            {
                if (Time.time - _lastToggleTime < ToggleCooldown)
                {
                    ShowMessage("[QFP] Please wait before toggling again.");
                    return;
                }
                _lastToggleTime = Time.time;

                if (State == ModState.IDLE)
                    TryEnterFreeplay();
                else if (State == ModState.IN_FREEPLAY)
                    TryReturnToMatch();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void TryEnterFreeplay()
        {
            if (State != ModState.IDLE)
            {
                ShowMessage("[QFP] Already in Quick Freeplay mode.");
                return;
            }

            var lobbyMgr = LobbyManager.instance;
            if (lobbyMgr == null)
            {
                ShowMessage("[QFP] No active lobby.");
                return;
            }

            var vc = lobbyMgr.CurrentGameController as VersusControl;
            if (vc == null)
            {
                ShowMessage("[QFP] Quick Freeplay only works during a party match.");
                return;
            }

            var phase = vc.Phase;
            if (phase == GameControl.GamePhase.NONE || phase == GameControl.GamePhase.START)
            {
                ShowMessage("[QFP] The match hasn't started yet.");
                return;
            }
            if (phase == GameControl.GamePhase.END || phase == GameControl.GamePhase.WAIT)
            {
                ShowMessage("[QFP] The match has already ended.");
                return;
            }

            if (lobbyMgr.IsInOnlineGame && !lobbyMgr.IsHost)
            {
                if (QuickFreeplayConfig.WarnInOnlineGame)
                    ShowMessage("[QFP] Only the host can use Quick Freeplay in online games.");
                return;
            }
            if (!lobbyMgr.IsHost)
            {
                ShowMessage("[QFP] Quick Freeplay requires host privileges.");
                return;
            }

            SavedSnapshot = CaptureSnapshot(vc);
            QuickFreeplayPlugin.Log.LogInfo(
                $"[QFP] Snapshot captured — Round {SavedSnapshot.RoundNumber}/{SavedSnapshot.MaxRounds}, " +
                $"{SavedSnapshot.PlacedBlocks.Count} blocks, scene: {SavedSnapshot.AssociatedScene}");

            // Save the current match state as a loadable in-game snapshot.
            SaveBackup(SavedSnapshot.RoundNumber);

            GameSettings.GetInstance().GameMode = GameState.GameMode.FREEPLAY;
            State = ModState.IN_FREEPLAY;
            // Inject the level XML so OnSetupStartLevel loads it on the new scene.
            // levelPortalXml is a public static field — it survives the scene reload.
            QuickSaver.levelPortalXml = SavedSnapshot.LevelXml;
            // Prepare all clients for scene reload: sync mode, show loading splash,
            // and reset sceneInitDone flags (without this, WaitForPlayers hangs forever).
            PrepareClientsForReload(GameState.GameMode.FREEPLAY);
            LoadingInterstitialSplash.Instance?.FadeIn();
            Placeable.SetInitialSequenceID(0);
            NetworkManager.singleton.ServerChangeScene(SavedSnapshot.AssociatedScene);
        }

        public void TryReturnToMatch()
        {
            if (SavedSnapshot == null)
            {
                State = ModState.IDLE;
                return;
            }

            var lobbyMgr = LobbyManager.instance;
            if (lobbyMgr != null && lobbyMgr.IsInOnlineGame && !lobbyMgr.IsHost)
            {
                ShowMessage("[QFP] Only the host can return to the match.");
                return;
            }

            QuickFreeplayPlugin.Log.LogInfo("[QFP] Returning to match...");

            // Capture current freeplay level state (includes any changes the player made).
            var fpc = lobbyMgr?.CurrentGameController as FreePlayControl;
            if (fpc != null)
            {
                string freshXml = GetLiveLevelXml(fpc);
                if (!string.IsNullOrEmpty(freshXml))
                {
                    SavedSnapshot.LevelXml = freshXml;
                    QuickFreeplayPlugin.Log.LogInfo("[QFP] Captured freeplay level state for match restore.");
                }
            }

            // Save the current freeplay state as a loadable in-game snapshot before leaving.
            SaveBackup(SavedSnapshot.RoundNumber);

            GameSettings.GetInstance().GameMode = GameState.GameMode.PARTY;
            State = ModState.RESTORING;
            QuickSaver.levelPortalXml = SavedSnapshot.LevelXml;
            // Prepare all clients for scene reload, then load our saved scene.
            // MaxRounds sync to non-host happens in DelayedScoreRestore during PLACE phase
            // (sending MsgApplyRuleset during freeplay corrupts non-host RuleBook UI).
            PrepareClientsForReload(GameState.GameMode.PARTY);
            LoadingInterstitialSplash.Instance?.FadeIn();
            Placeable.SetInitialSequenceID(0);
            NetworkManager.singleton.ServerChangeScene(SavedSnapshot.AssociatedScene);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Called by Harmony patches after game controllers finish SetupStart
        // ─────────────────────────────────────────────────────────────────────

        public void OnVersusControlReady(VersusControl vc)
        {
            if (State != ModState.RESTORING || SavedSnapshot == null)
                return;

            try
            {
                // Copy point blocks before nulling snapshot — the delayed coroutine needs them.
                var pointBlocks = new List<PointBlockData>(SavedSnapshot.PointBlockHistory);
                RestoreState(vc);
                int remaining = SavedSnapshot.MaxRounds - SavedSnapshot.RoundNumber + 1;
                QuickFreeplayPlugin.Log.LogInfo(
                    $"[QFP] Match restored — {remaining} rounds remaining (was round {SavedSnapshot.RoundNumber}/{SavedSnapshot.MaxRounds}).");
                ShowMessage($"[QFP] Match restored. {remaining} rounds remaining.");

                // Start delayed score restore BEFORE nulling snapshot — waits for PLACE phase
                // so all clients' ScoreKeepers are ready to receive MsgPointAwarded.
                // Suppress party box so scoreboard animation plays first.
                if (pointBlocks.Count > 0)
                {
                    SuppressPartyBox = true;
                    StartCoroutine(DelayedScoreRestore(vc, pointBlocks,
                        SavedSnapshot.RoundNumber, SavedSnapshot.MaxRounds));
                }

                SavedSnapshot = null;
                State = ModState.IDLE;
            }
            catch (Exception e)
            {
                QuickFreeplayPlugin.Log.LogError($"[QFP] Restore failed: {e}");
                State = ModState.IDLE;
            }
        }

        public void OnFreePlayControlReady(FreePlayControl fc)
        {
            if (State != ModState.IN_FREEPLAY) return;

            var snap = SavedSnapshot;
            if (snap == null) return;

            ShowMessage(
                $"[QFP] Freeplay active. Match saved at Round {snap.RoundNumber}/{snap.MaxRounds}. " +
                $"Press [{QuickFreeplayConfig.HotkeyToggle}] or click the button to return.",
                duration: 6f);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Snapshot capture
        // ─────────────────────────────────────────────────────────────────────

        private static MatchStateSnapshot CaptureSnapshot(VersusControl vc)
        {
            var snap = new MatchStateSnapshot
            {
                RoundNumber = (int)ReflectionCache.RoundNumber.GetValue(vc),
                MaxRounds   = GameSettings.GetInstance().MaxRounds,
                PhaseAtCapture = vc.Phase,
                AssociatedScene = vc.AssociatedScene,
                CapturedAt = DateTime.Now,
                // Capture the full live level state (base blocks + player-placed blocks).
                // Falls back to lastLoadedXml if the live serialisation fails.
                LevelXml = GetLiveLevelXml(vc) ?? QuickSaver.lastLoadedXml,
            };

            // Score breakdown — capture ALL PointBlocks (tallied history + current-round pending)
            var scoreKeeper = ScoreKeeper.Instance;
            if (scoreKeeper != null)
            {
                var history = ReflectionCache.HistoryPointBlocks.GetValue(scoreKeeper) as List<PointBlock>;
                int histCount = history?.Count ?? -1;
                int newCount = scoreKeeper.newPointBlocks?.Count ?? -1;
                QuickFreeplayPlugin.Log.LogInfo($"[QFP] Capture: ScoreKeeper found. historyPointBlocks={histCount}, newPointBlocks={newCount}");

                if (history != null)
                    foreach (var pb in history)
                        snap.PointBlockHistory.Add(new PointBlockData { Type = (int)pb.type, PlayerNumber = pb.playerNumber });

                foreach (var pb in scoreKeeper.newPointBlocks)
                    snap.PointBlockHistory.Add(new PointBlockData { Type = (int)pb.type, PlayerNumber = pb.playerNumber });

                QuickFreeplayPlugin.Log.LogInfo($"[QFP] Capture: Total PointBlocks saved = {snap.PointBlockHistory.Count}");
            }
            else
            {
                QuickFreeplayPlugin.Log.LogWarning("[QFP] Capture: ScoreKeeper.Instance is NULL!");
            }

            // Player-placed blocks
            var blocks = ReflectionCache.PlacedBlocks.GetValue(vc) as List<Placeable>;
            if (blocks != null)
            {
                foreach (var b in blocks)
                {
                    if (b == null) continue;
                    snap.PlacedBlocks.Add(new PlacedBlockData
                    {
                        PrefabID   = b.ID,
                        Position   = b.transform.position,
                        LocalScale = b.transform.localScale,
                        Rotation   = b.transform.rotation,
                    });
                }
            }

            return snap;
        }

        // ─────────────────────────────────────────────────────────────────────
        // State restoration
        // ─────────────────────────────────────────────────────────────────────

        private void RestoreState(VersusControl vc)
        {
            // Set MaxRounds to the REMAINING count (belt-and-suspenders with VersusControlPatch Prefix).
            // roundNumber is left at 0 — ToPlaceMode increments to 1 naturally.
            // lastRoundsMode/lastRoundsToGo are handled naturally by the game with the reduced MaxRounds.
            int remaining = SavedSnapshot.MaxRounds - SavedSnapshot.RoundNumber + 1;
            if (remaining < 1) remaining = 1;
            GameSettings.GetInstance().MaxRounds = remaining;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Waits for PLACE phase (all clients loaded, ScoreKeepers initialized, UI ready),
        /// then restores scores and syncs reduced MaxRounds to non-host clients.
        ///
        /// Score restore uses NetworkServer.SendToAll for MsgPointAwarded so the messages
        /// go through the game's full event pipeline (distributeMessage → NetworkMessageReceivedEvent).
        /// This is critical for UCHScoreboard compatibility — its OnPointScored handler only
        /// fires from NetworkMessageReceivedEvent, not from direct newPointBlocks injection.
        ///
        /// We do NOT call TallyPointBlockAllPlayers manually. Points sit in newPointBlocks
        /// on all clients until the game's natural RpcShowScoreboard fires at the end of the
        /// next round, which tallies everything (restored + new round's points) at once.
        /// </summary>
        private IEnumerator DelayedScoreRestore(VersusControl vc, List<PointBlockData> pointBlocks,
                                                 int savedRoundNumber, int originalMaxRounds)
        {
            float timeout = 15f;
            while (vc != null && vc.Phase != GameControl.GamePhase.PLACE && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (timeout <= 0f)
                QuickFreeplayPlugin.Log.LogWarning("[QFP] DelayedScoreRestore: Timed out waiting for PLACE phase.");
            yield return new WaitForSeconds(0.3f);

            if (vc == null || !NetworkServer.active)
            {
                QuickFreeplayPlugin.Log.LogWarning(
                    $"[QFP] DelayedScoreRestore: Aborted (vc={vc != null}, server={NetworkServer.active}).");
                yield break;
            }

            // ── 0. Recalculate levelDensity so the first party box sees the correct
            //       block density instead of the stale 0 from a brand-new session.
            //       blocksArea is already correct (AddBlock is called during XML load),
            //       but the game only derives levelDensity during the PLAY-phase transition,
            //       which hasn't happened yet. We compute it ourselves now.
            if (savedRoundNumber >= 2 && vc != null)
            {
                try
                {
                    float area      = (float)ReflectionCache.BlocksArea.GetValue(vc);
                    float totalArea = vc.LevelLayout?.ComputedTotalArea ?? 0f;
                    if (totalArea > 0f)
                    {
                        float density = area / totalArea;
                        ReflectionCache.LevelDensity.SetValue(vc, density);
                        QuickFreeplayPlugin.Log.LogInfo(
                            $"[QFP] levelDensity recalculated: {density:F3} " +
                            $"(blocksArea={area:F1}, totalArea={totalArea:F1})");
                    }
                }
                catch (Exception e)
                {
                    QuickFreeplayPlugin.Log.LogWarning($"[QFP] Density recalc failed: {e.Message}");
                }
            }

            try
            {
                // ── 1. Sync reduced MaxRounds to non-host clients only ──
                SyncReducedMaxRoundsToClients(savedRoundNumber, originalMaxRounds);
            }
            catch (Exception e)
            {
                QuickFreeplayPlugin.Log.LogError($"[QFP] DelayedScoreRestore failed: {e}");
                yield break;
            }

            // ── 2. Wait 3 seconds for UCHScoreboard to initialize before sending points ──
            yield return new WaitForSeconds(3f);

            if (vc == null || !NetworkServer.active) yield break;

            try
            {
                // ── 3. Send MsgPointAwarded to ALL clients (including host) ──
                foreach (var pbd in pointBlocks)
                {
                    var msg = new MsgPointAwarded();
                    msg.PlayerNumber = pbd.PlayerNumber;
                    msg.PointType = (PointBlock.pointBlockType)pbd.Type;
                    msg.AlwaysAward = GameSettings.GetInstance().AlwaysAwardPointType(msg.PointType);
                    NetworkServer.SendToAll(NetMsgTypes.PointAwarded, msg);
                }
                QuickFreeplayPlugin.Log.LogInfo(
                    $"[QFP] DelayedScoreRestore: Sent {pointBlocks.Count} MsgPointAwarded to all clients.");
            }
            catch (Exception e)
            {
                QuickFreeplayPlugin.Log.LogError($"[QFP] DelayedScoreRestore failed: {e}");
                yield break;
            }

            // ── 4. Wait 1 second for non-host clients to process MsgPointAwarded ──
            // Without this delay, non-host newPointBlocks is empty when the scoreboard fires.
            yield return new WaitForSeconds(1f);

            // ── 5. Trigger scoreboard BEFORE the party box opens ──
            float scoreboardTime = 5f;
            if (vc != null && NetworkServer.active)
            {
                vc.CallRpcShowScoreboard(scoreboardTime, false, false);
                QuickFreeplayPlugin.Log.LogInfo("[QFP] DelayedScoreRestore: Triggered RpcShowScoreboard for tally.");
            }

            // Wait for scoreboard animation to finish
            yield return new WaitForSeconds(scoreboardTime + 0.5f);

            // Now open the party box
            SuppressPartyBox = false;
            if (vc != null && ReflectionCache.ShowPartyBox != null)
            {
                ReflectionCache.ShowPartyBox.Invoke(vc, null);
                QuickFreeplayPlugin.Log.LogInfo("[QFP] DelayedScoreRestore: Party box opened after scoreboard.");
            }
        }

        /// <summary>
        /// Sends a network message to all connected clients EXCEPT the host's local connection.
        /// connectionId 0 is always the host's local (loopback) connection in UNET.
        /// </summary>
        private static void SendToNonHostClients(short msgType, MessageBase msg)
        {
            for (int i = 0; i < NetworkServer.connections.Count; i++)
            {
                var conn = NetworkServer.connections[i];
                if (conn != null && conn.connectionId != 0)
                    conn.Send(msgType, msg);
            }
        }

        /// <summary>
        /// Pushes the remaining MaxRounds to NON-HOST clients via MsgApplyRuleset
        /// so they (and UCHScoreboard) see the correct number of rounds.
        /// Must be called during PLACE phase so the RuleBook UI is properly initialized.
        /// Host already has the correct MaxRounds from VersusControlPatch Prefix.
        /// </summary>
        private void SyncReducedMaxRoundsToClients(int roundNumber, int originalMaxRounds)
        {
            int remaining = originalMaxRounds - roundNumber + 1;
            if (remaining < 1) remaining = 1;

            // Host's MaxRounds is already set to 'remaining' by VersusControlPatch Prefix.
            // LoadRulesFromSettings will pick up the current value.
            try
            {
                var preset = ScriptableObject.CreateInstance<GameRulePreset>();
                preset.IsPremade = false;
                preset.LoadRulesFromSettings();

                var msg = new MsgApplyRuleset();
                msg.rulesetXML = preset.GetRulesetXmlString();
                msg.premadeIdx = -1;
                msg.applyRules = true;
                msg.applyPoints = false;
                msg.applyBlocks = false;
                msg.applyMods = false;
                msg.temporary = true;

                SendToNonHostClients(NetMsgTypes.ApplyRuleset, msg);
                UnityEngine.Object.Destroy(preset);

                QuickFreeplayPlugin.Log.LogInfo(
                    $"[QFP] Synced MaxRounds={remaining} to non-host clients (original={originalMaxRounds}, round={roundNumber}).");
            }
            catch (Exception e)
            {
                QuickFreeplayPlugin.Log.LogError($"[QFP] SyncReducedMaxRoundsToClients failed: {e}");
            }
        }

        /// <summary>
        /// Sends MsgPrepareToReloadScene to all clients, replicating what the game's
        /// FadeAndReloadScene coroutine does before calling ServerChangeScene.
        /// This ensures clients: update their GameMode, show a loading splash, and
        /// reset their sceneInitDone flags (without which WaitForPlayers hangs forever).
        /// </summary>
        private static void PrepareClientsForReload(GameState.GameMode toMode)
        {
            var msg = new MsgPrepareToReloadScene
            {
                reloadToMode = toMode,
                snapshotInfo = GameState.GetInstance().currentSnapshotInfo,
            };
            NetworkServer.SendToAll(NetMsgTypes.PrepareToReloadScene, msg);
        }

        /// <summary>
        /// Calls QuickSaver.GetCurrentXmlSnapshot on the QuickSaver component attached to
        /// the given controller and returns the serialised XML string.
        /// Returns null if QuickSaver or the method result is unavailable.
        /// </summary>
        private static string GetLiveLevelXml(GameControl controller)
        {
            try
            {
                // Prefer the instance attached to the controller; fall back to lastInstance.
                var qs = controller.GetComponent<QuickSaver>()
                      ?? QuickSaver.lastInstance;

                if (qs == null) return null;

                var xmlDoc = ReflectionCache.GetCurrentXmlSnapshot.Invoke(qs, new object[] { false }) as XmlDocument;
                return xmlDoc?.OuterXml;
            }
            catch (Exception e)
            {
                QuickFreeplayPlugin.Log.LogWarning($"[QFP] GetLiveLevelXml failed: {e.Message}");
                return null;
            }
        }

        private static readonly System.Random _backupRng = new System.Random();

        /// <summary>
        /// Saves the current live scene as a loadable in-game .snapshot file using
        /// QuickSaver.DoLocalSave, then captures a thumbnail so the save displays
        /// correctly in the game's snapshot browser.
        /// </summary>
        private static void SaveBackup(int roundNumber)
        {
            var qs = QuickSaver.lastInstance;
            if (qs == null)
            {
                QuickFreeplayPlugin.Log.LogWarning("[QFP] Backup skipped — no QuickSaver instance.");
                return;
            }
            try
            {
                string date = DateTime.Now.ToString("dd.MM.yyyy");
                int rand = _backupRng.Next(10000, 99999);
                string saveName = $"QFP Backup {date} {rand}";

                qs.DoLocalSave(
                    saveName,
                    FeaturedQuickFilter.LevelTypes.Versus,
                    1,
                    false,
                    (success, filename) =>
                    {
                        if (success)
                        {
                            QuickFreeplayPlugin.Log.LogInfo($"[QFP] Backup saved: {filename}");
                            // Save a thumbnail so the snapshot shows a preview in the game browser.
                            // The thumbnail name must match the save name (without the type suffix).
                            try { qs.SaveLocalThumbnail(saveName); }
                            catch (Exception te)
                            {
                                QuickFreeplayPlugin.Log.LogWarning($"[QFP] Thumbnail failed: {te.Message}");
                            }
                        }
                        else
                            QuickFreeplayPlugin.Log.LogWarning("[QFP] Backup save failed.");
                    });
            }
            catch (Exception e)
            {
                QuickFreeplayPlugin.Log.LogWarning($"[QFP] Backup exception: {e.Message}");
            }
        }

        public void ResetState()
        {
            StopAllCoroutines();
            State            = ModState.IDLE;
            SavedSnapshot    = null;
            SuppressPartyBox = false;
        }

        public void ShowMessage(string msg, float duration = 4f)
        {
            _screenMessage = msg;
            _messageExpiry = Time.time + duration;
        }

        public string GetCurrentMessage()
        {
            if (string.IsNullOrEmpty(_screenMessage) || Time.time > _messageExpiry)
                return null;
            return _screenMessage;
        }
    }
}
