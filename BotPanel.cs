using System.Reflection;
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
        private const float ButtonHeight = 28f;
        private const int ButtonCount = 2;
        private const float StatsLineHeight = 18f;
        private const int StatsLineCount = 2;

        private static readonly FieldInfo IgnoreMouseField = typeof(MouseButtonManager).GetField(
            "ignoreMouse",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly BotLogger _logger;
        private readonly InGameConsole _console;
        private Rect _window = new Rect(40f, 60f, WindowWidth, ComputeWindowHeight());
        private MouseButtonManager _mouseManager;
        private bool _setIgnoreMouse;

        public BotPanel(ManualLogSource log)
        {
            _console = new InGameConsole();
            _logger = new BotLogger(log, _console);
            _logger.LogInfo("Панель бота готова.");
        }

        public bool Visible { get; set; }

        public void Toggle()
        {
            Visible = !Visible;
            if (!Visible)
                ReleaseInputBlock();
        }

        public void ReleaseInputBlock()
        {
            SetIgnoreMouse(false);
        }

        public void Draw()
        {
            if (!Visible)
                return;

            _window.height = ComputeWindowHeight();
            _window = GUI.Window(WindowId, _window, DrawWindow, "MistLegacy Bot");
            ConsumeMouseOverWindow();
        }

        public void UpdateInputBlock()
        {
            var block = Visible && IsMouseOverWindow();
            SetIgnoreMouse(block);
        }

        private bool IsMouseOverWindow()
        {
            var mouse = Input.mousePosition;
            var guiPoint = new Vector2(mouse.x, Screen.height - mouse.y);
            return _window.Contains(guiPoint);
        }

        private void ConsumeMouseOverWindow()
        {
            var ev = UnityEngine.Event.current;
            if (ev == null || !IsMouseOverWindow())
                return;

            if (ev.type == EventType.Layout || ev.type == EventType.Repaint)
                return;

            if (ev.isMouse || ev.type == EventType.ScrollWheel)
                ev.Use();
        }

        private void SetIgnoreMouse(bool ignore)
        {
            if (IgnoreMouseField == null)
                return;

            if (_mouseManager == null)
                _mouseManager = Object.FindObjectOfType<MouseButtonManager>();

            if (_mouseManager == null)
                return;

            if (ignore)
            {
                IgnoreMouseField.SetValue(_mouseManager, true);
                _setIgnoreMouse = true;
            }
            else if (_setIgnoreMouse)
            {
                IgnoreMouseField.SetValue(_mouseManager, false);
                _setIgnoreMouse = false;
            }
        }

        private static float ComputeWindowHeight()
        {
            const float topPad = 4f;
            const float buttons = ButtonHeight * ButtonCount;
            const float midPad = 6f;
            const float hintLabel = 20f;
            const float statsBlock = StatsLineHeight * StatsLineCount + 4f;
            const float beforeConsole = 4f;
            const float skinExtra = 16f;
            return WindowTitleHeight
                   + topPad
                   + buttons
                   + midPad
                   + hintLabel
                   + statsBlock
                   + beforeConsole
                   + InGameConsole.LayoutHeight
                   + WindowBottomSlack
                   + skinExtra;
        }

        private void DrawWindow(int id)
        {
            GUILayout.Space(4f);

            if (GUILayout.Button("Gather", GUILayout.Height(ButtonHeight)))
                GatherService.TryGatherNearest(_logger);

            if (GUILayout.Button("Stats", GUILayout.Height(ButtonHeight)))
                ResourceStatsService.DumpToLog(_logger);

            GUILayout.Space(6f);
            GUILayout.Label("F8 — показать / скрыть");
            DrawLiveStats();

            GUILayout.Space(4f);
            _console.Draw();
            GUILayout.Space(WindowBottomSlack);

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static void DrawLiveStats()
        {
            var snap = ResourceStatsService.Capture();
            if (!snap.Ok)
            {
                GUILayout.Label(snap.Error, GUILayout.Height(StatsLineHeight));
                GUILayout.Label(string.Empty, GUILayout.Height(StatsLineHeight));
                return;
            }

            GUILayout.Label(
                $"Узлы {snap.Total}  вид {snap.Visible}  gather {snap.CanGather}  туман {snap.Unknown}",
                GUILayout.Height(StatsLineHeight));
            GUILayout.Label(
                $"Сумка {snap.BagFree}/{snap.BagCapacity}  типов {snap.Types.Count}",
                GUILayout.Height(StatsLineHeight));
        }
    }
}
