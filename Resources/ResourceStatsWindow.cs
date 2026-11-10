using System.Collections.Generic;
using UnityEngine;

namespace MistLegacyBot
{
    internal sealed class ResourceStatsWindow
    {
        private const int WindowId = 0x4D4C43;
        private static readonly string[] GroupLabels = { "Без групп", "Навык", "Регион", "Редкость", "Тир" };

        private Rect _window = new Rect(580f, 60f, 780f, 420f);
        private Vector2 _scroll;
        private int _groupIndex;
        private GUIStyle _headerStyle;
        private GUIStyle _groupStyle;
        private GUIStyle _cellStyle;

        public bool Visible { get; set; }

        public Rect WindowRect
        {
            get { return _window; }
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
                ResourceCatalog.Instance.Refresh();
        }

        public void Draw()
        {
            if (!Visible)
                return;

            _window = GUI.Window(WindowId, _window, DrawWindow, "Ресурсы");
        }

        private void DrawWindow(int id)
        {
            EnsureStyles();
            var catalog = ResourceCatalog.Instance;

            GUILayout.BeginHorizontal();
            _groupIndex = GUILayout.Toolbar(_groupIndex, GroupLabels, GUILayout.Height(24f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Обновить", GUILayout.Width(90f), GUILayout.Height(24f)))
                catalog.Refresh();
            if (GUILayout.Button("Закрыть", GUILayout.Width(80f), GUILayout.Height(24f)))
                Visible = false;
            GUILayout.EndHorizontal();

            if (!catalog.Ok)
            {
                GUILayout.Label(catalog.Error ?? "Нет данных.");
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }

            GUILayout.Label(
                "Узлов " + catalog.Nodes.Count +
                "  live " + catalog.LiveCount +
                "  вид " + catalog.VisibleCount +
                "  gather " + catalog.CanGatherCount +
                "  туман " + catalog.Unknown +
                "  сумка " + catalog.BagFree + "/" + catalog.BagCapacity);

            DrawHeader();
            _scroll = GUILayout.BeginScrollView(_scroll);
            var mode = (ResourceGroupMode)_groupIndex;
            foreach (var group in catalog.Grouped(mode))
            {
                if (mode != ResourceGroupMode.None)
                    GUILayout.Label(group.Key + "  (" + Count(group) + ")", _groupStyle);

                foreach (var row in group)
                    DrawRow(row);
            }
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(_headerStyle);
            Cell("Имя", 140f, _headerStyle);
            Cell("Редкость", 80f, _headerStyle);
            Cell("Тир", 40f, _headerStyle);
            Cell("Регион", 130f, _headerStyle);
            Cell("Навык", 140f, _headerStyle);
            Cell("Qty", 40f, _headerStyle);
            Cell("Откат", 110f, _headerStyle);
            Cell("Вид", 70f, _headerStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawRow(ResourceRecord row)
        {
            GUILayout.BeginHorizontal();
            Cell(row.Name, 140f, _cellStyle);
            Cell(row.Rarity, 80f, _cellStyle);
            Cell(row.TierLabel, 40f, _cellStyle);
            Cell(row.Region, 130f, _cellStyle);
            Cell(row.SkillLabel, 140f, _cellStyle);
            Cell(row.Quantity.HasValue ? row.Quantity.Value.ToString() : "-", 40f, _cellStyle);
            Cell(row.Cooldown, 110f, _cellStyle);
            Cell(row.VisibleLabel, 70f, _cellStyle);
            GUILayout.EndHorizontal();
        }

        private static void Cell(string text, float width, GUIStyle style)
        {
            GUILayout.Label(text ?? "", style, GUILayout.Width(width), GUILayout.Height(18f));
        }

        private static int Count(IEnumerable<ResourceRecord> group)
        {
            var n = 0;
            foreach (var _ in group)
                n++;
            return n;
        }

        private void EnsureStyles()
        {
            if (_cellStyle != null)
                return;

            _cellStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                clipping = TextClipping.Clip,
                wordWrap = false
            };

            _headerStyle = new GUIStyle(_cellStyle)
            {
                fontStyle = FontStyle.Bold
            };

            _groupStyle = new GUIStyle(_cellStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.45f) }
            };
        }
    }
}
