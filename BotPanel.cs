using BepInEx.Logging;
using UnityEngine;

namespace MistLegacyBot
{
    internal sealed class BotPanel
    {
        private const int WindowId = 0x4D4C42;
        private const float WindowWidth = 520f;
        private const float WindowTitleHeight = 24f;
        private const float WindowBottomSlack = 20f;

        private readonly BotLogger _logger;
        private readonly InGameConsole _console;
        private Rect _window = new Rect(40f, 60f, WindowWidth, ComputeWindowHeight());

        public BotPanel(ManualLogSource log)
        {
            _console = new InGameConsole();
            _logger = new BotLogger(log, _console);
            _logger.LogInfo("Панель бота готова.");
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

            _window.height = ComputeWindowHeight();
            _window = GUI.Window(WindowId, _window, DrawWindow, "MistLegacy Bot");
        }

        private static float ComputeWindowHeight()
        {
            const float topPad = 4f;
            const float buttons = 28f * 4f;
            const float midPad = 8f + 6f;
            const float hintLabel = 20f;
            const float beforeConsole = 4f;
            return WindowTitleHeight
                   + topPad
                   + buttons
                   + midPad
                   + hintLabel
                   + beforeConsole
                   + InGameConsole.LayoutHeight
                   + WindowBottomSlack;
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(4f);

            if (GUILayout.Button("Start", GUILayout.Height(28f)))
                _logger.LogInfo("Start clicked.");

            if (GUILayout.Button("Stop", GUILayout.Height(28f)))
                _logger.LogInfo("Stop clicked.");

            GUILayout.Space(8f);

            if (GUILayout.Button("Gather", GUILayout.Height(28f)))
                GatherService.TryGatherNearest(_logger);

            if (GUILayout.Button("Fight", GUILayout.Height(28f)))
                _logger.LogInfo("Fight clicked.");

            GUILayout.Space(6f);
            GUILayout.Label("F8 — показать / скрыть");

            GUILayout.Space(4f);
            _console.Draw();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }
    }
}
