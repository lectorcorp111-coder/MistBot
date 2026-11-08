using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.EventSystems;

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
        private readonly ResourceStatsWindow _statsWindow = new ResourceStatsWindow();
        private Rect _window = new Rect(40f, 60f, WindowWidth, ComputeWindowHeight());
        private MouseButtonManager _mouseManager;
        private bool _setIgnoreMouse;
        private EventSystem _pausedEventSystem;

        public BotPanel(ManualLogSource log)
        {
            _console = new InGameConsole();
            _logger = new BotLogger(log, _console);
            _logger.LogInfo("Панель бота готова.");
        }

        // Hidden until F8 so the login screen stays clickable.
        public bool Visible { get; set; } = true;

        public void Toggle()
        {
            Visible = !Visible;
            if (!Visible)
            {
                _statsWindow.Visible = false;
                ReleaseInputBlock();
            }
        }

        public void ReleaseInputBlock()
        {
            SetIgnoreMouse(false);
            PauseEventSystem(false);
        }

        public void Draw()
        {
            if (!Visible)
                return;

            ResourceCatalog.Instance.Refresh();

            _window.height = ComputeWindowHeight();
            _window = GUI.Window(WindowId, _window, DrawWindow, "MistLegacy Bot");
            _statsWindow.Draw();
            ConsumeMouseOverOverlay();
        }

        public void UpdateInputBlock()
        {
            var block = Visible && IsMouseOverOverlay();
            SetIgnoreMouse(block);
            PauseEventSystem(block);
            if (block)
                Input.ResetInputAxes();
        }

        private bool IsMouseOverOverlay()
        {
            var mouse = Input.mousePosition;
            var guiPoint = new Vector2(mouse.x, Screen.height - mouse.y);
            if (_window.Contains(guiPoint))
                return true;
            return _statsWindow.Visible && _statsWindow.WindowRect.Contains(guiPoint);
        }

        private void ConsumeMouseOverOverlay()
        {
            var ev = UnityEngine.Event.current;
            if (ev == null || !IsMouseOverOverlay())
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

        private void PauseEventSystem(bool pause)
        {
            if (pause)
            {
                var es = EventSystem.current != null ? EventSystem.current : _pausedEventSystem;
                if (es == null)
                    es = Object.FindObjectOfType<EventSystem>();
                if (es == null)
                    return;

                _pausedEventSystem = es;
                if (es.enabled)
                    es.enabled = false;
            }
            else if (_pausedEventSystem != null)
            {
                _pausedEventSystem.enabled = true;
                _pausedEventSystem = null;
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
                _statsWindow.Toggle();

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
            var catalog = ResourceCatalog.Instance;
            if (!catalog.Ok)
            {
                GUILayout.Label(catalog.Error ?? "Нет данных.", GUILayout.Height(StatsLineHeight));
                GUILayout.Label(string.Empty, GUILayout.Height(StatsLineHeight));
                return;
            }

            GUILayout.Label(
                $"Узлы {catalog.Nodes.Count}  вид {catalog.VisibleCount}  gather {catalog.CanGatherCount}  туман {catalog.Unknown}",
                GUILayout.Height(StatsLineHeight));
            GUILayout.Label(
                $"Сумка {catalog.BagFree}/{catalog.BagCapacity}  live {catalog.LiveCount}",
                GUILayout.Height(StatsLineHeight));
        }
    }
}
