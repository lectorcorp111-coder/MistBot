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
        private const int ButtonCount = 1;
        private const float StatsLineHeight = 18f;
        private const int StatsLineCount = 2;

        private readonly BotLogger _logger;
        private readonly InGameConsole _console;
        private readonly OverlayInput _input = new OverlayInput();
        private readonly ResourceStatsWindow _statsWindow = new ResourceStatsWindow();
        private readonly RegionListWindow _regionWindow = new RegionListWindow();
        private Rect _window = new Rect(40f, 60f, WindowWidth, ComputeWindowHeight());

        public BotPanel(ManualLogSource log)
        {
            _console = new InGameConsole();
            _logger = new BotLogger(log, _console);
            _logger.LogInfo("Панель бота готова. v" + Plugin.PluginVersion);
        }

        public bool Visible { get; set; } = true;

        public void Toggle()
        {
            Visible = !Visible;
            if (!Visible)
            {
                _statsWindow.Visible = false;
                _regionWindow.Visible = false;
                _input.Release();
            }
        }

        public void ReleaseInputBlock()
        {
            _input.Release();
        }

        public void Draw()
        {
            if (!Visible)
                return;

            ResourceCatalog.Instance.Refresh();

            _window.height = ComputeWindowHeight();
            _window = GUI.Window(WindowId, _window, DrawWindow, "MistLegacy Bot  " + Plugin.PluginVersion);
            _statsWindow.Draw();
            _regionWindow.Draw();
            OverlayInput.ConsumeIfOver(IsMouseOverOverlay());
        }

        public void UpdateInputBlock()
        {
            _input.Update(Visible && IsMouseOverOverlay());
        }

        private bool IsMouseOverOverlay()
        {
            var guiPoint = OverlayInput.GuiMouse();
            if (OverlayInput.ContainsGuiPoint(_window, guiPoint))
                return true;
            if (_statsWindow.Visible && OverlayInput.ContainsGuiPoint(_statsWindow.WindowRect, guiPoint))
                return true;
            return _regionWindow.Visible && OverlayInput.ContainsGuiPoint(_regionWindow.WindowRect, guiPoint);
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

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Gather", GUILayout.Height(ButtonHeight)))
                GatherService.TryGatherNearest(_logger);
            if (GUILayout.Button("Stats", GUILayout.Height(ButtonHeight)))
                _statsWindow.Toggle();
            if (GUILayout.Button("Регионы", GUILayout.Height(ButtonHeight)))
                _regionWindow.Toggle();
            GUILayout.EndHorizontal();

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
