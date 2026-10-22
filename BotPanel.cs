using BepInEx.Logging;
using UnityEngine;

namespace MistLegacyBot
{
    internal sealed class BotPanel
    {
        private const int WindowId = 0x4D4C42;

        private readonly ManualLogSource _log;
        private Rect _window = new Rect(40f, 80f, 260f, 240f);

        public BotPanel(ManualLogSource log)
        {
            _log = log;
        }

        public bool Visible { get; set; } = true;

        public void Toggle()
        {
            Visible = !Visible;
        }

        public void Draw()
        {
            if (!Visible)
                return;

            _window = GUI.Window(WindowId, _window, DrawWindow, "MistLegacy Bot");
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(4f);

            if (GUILayout.Button("Start", GUILayout.Height(28f)))
                _log.LogInfo("Start clicked.");

            if (GUILayout.Button("Stop", GUILayout.Height(28f)))
                _log.LogInfo("Stop clicked.");

            GUILayout.Space(8f);

            if (GUILayout.Button("Gather", GUILayout.Height(28f)))
                GatherService.TryGatherNearest(_log);

            if (GUILayout.Button("Fight", GUILayout.Height(28f)))
                _log.LogInfo("Fight clicked.");

            GUILayout.FlexibleSpace();
            GUILayout.Label("F8 — показать / скрыть");

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }
    }
}
