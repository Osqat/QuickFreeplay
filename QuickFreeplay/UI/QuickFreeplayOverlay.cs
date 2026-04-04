using UnityEngine;

namespace QuickFreeplay
{
    /// <summary>
    /// IMGUI overlay.  Renders on top of everything without any Canvas or prefab
    /// setup.  Attached to the same persistent GameObject as QuickFreeplayManager.
    /// </summary>
    public class QuickFreeplayOverlay : MonoBehaviour
    {
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _toastStyle;
        private bool _stylesBuilt;

        void OnGUI()
        {
            if (!_stylesBuilt) BuildStyles();

            var mgr = QuickFreeplayManager.Instance;
            if (mgr == null) return;

            // ── Timed toast message (bottom-left) ────────────────────────────
            string toast = mgr.GetCurrentMessage();
            if (toast != null)
            {
                var toastRect = new Rect(10, Screen.height - 60, 700, 45);
                DrawShadowedLabel(toastRect, toast, _toastStyle);
            }

            // ── State-specific HUD ───────────────────────────────────────────
            switch (mgr.State)
            {
                case QuickFreeplayManager.ModState.IDLE:
                    break;

                case QuickFreeplayManager.ModState.IN_FREEPLAY:
                    DrawFreeplayUI(mgr);
                    break;

                case QuickFreeplayManager.ModState.RESTORING:
                    DrawRestoringUI();
                    break;
            }
        }

        // ── IN_FREEPLAY: prominent "Return to Match" button + snapshot info ──

        void DrawFreeplayUI(QuickFreeplayManager mgr)
        {
            float cx = Screen.width / 2f;

            // Return button
            var btnRect = new Rect(cx - 130, 10, 260, 44);
            if (GUI.Button(btnRect, $"Return to Match  [{QuickFreeplayConfig.HotkeyToggle}]", _buttonStyle))
                mgr.TryReturnToMatch();

            // Snapshot info (top-left)
            var snap = mgr.SavedSnapshot;
            if (snap != null)
            {
                string info =
                    $"QUICK FREEPLAY\n" +
                    $"Round {snap.RoundNumber} / {snap.MaxRounds} saved\n" +
                    $"Captured {snap.CapturedAt:HH:mm:ss}  |  {snap.PlacedBlocks.Count} block(s)";
                DrawShadowedLabel(new Rect(10, 10, 320, 65), info, _labelStyle);
            }
        }

        // ── RESTORING: simple status label ───────────────────────────────────

        void DrawRestoringUI()
        {
            // Pulsing ellipsis
            int dots = (int)(Time.time * 2f) % 4;
            string label = "Restoring match" + new string('.', dots);
            DrawShadowedLabel(
                new Rect(Screen.width / 2f - 130, 10, 260, 32),
                label,
                _labelStyle);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// Draws a label with a 1px dark shadow for readability over game art.
        void DrawShadowedLabel(Rect r, string text, GUIStyle style)
        {
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), text, shadow);
            GUI.Label(r, text, style);
        }

        void BuildStyles()
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _buttonStyle.normal.textColor    = Color.white;
            _buttonStyle.hover.textColor     = Color.white;
            _buttonStyle.normal.background   = MakeTex(2, 2, new Color(0.75f, 0.35f, 0f, 0.92f));
            _buttonStyle.hover.background    = MakeTex(2, 2, new Color(1f,    0.50f, 0f, 0.97f));
            _buttonStyle.active.background   = MakeTex(2, 2, new Color(0.55f, 0.25f, 0f, 1.00f));
            _buttonStyle.active.textColor    = Color.white;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 13,
                fontStyle = FontStyle.Bold,
            };
            _labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.90f);

            _toastStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
            };
            _toastStyle.normal.textColor = new Color(1f, 0.95f, 0.4f, 1f); // yellow

            _stylesBuilt = true;
        }

        static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex    = new Texture2D(w, h);
            var pixels = new Color[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = col;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
