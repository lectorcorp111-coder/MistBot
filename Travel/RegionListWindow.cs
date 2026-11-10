using System;
using System.Collections.Generic;
using UnityEngine;
using Virtys.Common.Shared.Algorithms;
using Virtys.Nebula.Client.ViewModel.Domain;
using Virtys.ViewModel;

namespace MistLegacyBot
{
    internal sealed class RegionListWindow
    {
        private const int WindowId = 0x4D4C50;
        private static readonly string[] SortLabels = { "Дальность", "Название" };

        private Rect _window = new Rect(580f, 490f, 520f, 380f);
        private Vector2 _scroll;
        private int _sortIndex;
        private GUIStyle _headerStyle;
        private GUIStyle _cellStyle;
        private GUIStyle _hereStyle;

        public bool Visible { get; set; }

        public Rect WindowRect
        {
            get { return _window; }
        }

        public void Toggle()
        {
            Visible = !Visible;
        }

        public void Draw()
        {
            if (!Visible)
                return;

            _window = GUI.Window(WindowId, _window, DrawWindow, "Регионы");
        }

        private void DrawWindow(int id)
        {
            EnsureStyles();

            GUILayout.BeginHorizontal();
            _sortIndex = GUILayout.Toolbar(_sortIndex, SortLabels, GUILayout.Height(24f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Закрыть", GUILayout.Width(80f), GUILayout.Height(24f)))
                Visible = false;
            GUILayout.EndHorizontal();

            var player = PlayerVM.Current;
            var group = player != null ? player.Group : null;
            var world = WorldVM.Current;
            if (group == null || world == null || world.Regions == null)
            {
                GUILayout.Label("Нет отряда / мира.");
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }

            var here = world.GetRegion(group.X, group.Y, world.Region);
            GUILayout.Label(
                (here != null ? here.Name : "?") + "  (" + group.X + "," + group.Y + ")",
                _headerStyle);

            DrawHeader();
            _scroll = GUILayout.BeginScrollView(_scroll);

            var rows = BuildRows(world, group, here);
            foreach (var row in rows)
            {
                var style = row.Current ? _hereStyle : _cellStyle;
                GUILayout.BeginHorizontal();
                GUILayout.Label(row.Name, style, GUILayout.Width(260f), GUILayout.Height(18f));
                GUILayout.Label(row.DistText, _cellStyle, GUILayout.Width(70f), GUILayout.Height(18f));
                GUILayout.Label(row.AccessText, _cellStyle, GUILayout.Width(140f), GUILayout.Height(18f));
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private List<Row> BuildRows(WorldVM world, GroupVM group, RegionVM here)
        {
            var rows = new List<Row>();
            foreach (var region in world.Regions)
            {
                if (region == null || string.IsNullOrEmpty(region.Name))
                    continue;
                if (region.Inside || region.Dungeon)
                    continue;

                int x;
                int y;
                var dist = double.NaN;
                if (TryGetPoint(region, out x, out y))
                    dist = Distance.Measure(x, y, group.X, group.Y);

                SkillValueVM blockedSkill;
                UnitVM blockedUnit;
                var can = region.CanTravel(group);
                if (!can)
                    region.CanTravel(group, out blockedSkill, out blockedUnit);
                else
                {
                    blockedSkill = null;
                    blockedUnit = null;
                }

                rows.Add(new Row
                {
                    Name = region.Name,
                    Dist = dist,
                    DistText = double.IsNaN(dist) ? "-" : dist.ToString("F0"),
                    Can = can,
                    AccessText = FormatAccess(can, blockedSkill, blockedUnit),
                    Current = here != null && here.ID == region.ID
                });
            }

            if (_sortIndex == 1)
            {
                rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            }
            else
            {
                rows.Sort((a, b) =>
                {
                    var byCan = b.Can.CompareTo(a.Can);
                    if (byCan != 0)
                        return byCan;
                    var da = double.IsNaN(a.Dist) ? double.MaxValue : a.Dist;
                    var db = double.IsNaN(b.Dist) ? double.MaxValue : b.Dist;
                    var byDist = da.CompareTo(db);
                    if (byDist != 0)
                        return byDist;
                    return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                });
            }

            return rows;
        }

        private static string FormatAccess(bool can, SkillValueVM blockedSkill, UnitVM blockedUnit)
        {
            if (can)
                return "да";
            var skill = blockedSkill != null && blockedSkill.Skill != null ? blockedSkill.Skill.Name : null;
            var unit = blockedUnit != null ? blockedUnit.Name : null;
            if (!string.IsNullOrEmpty(skill) && !string.IsNullOrEmpty(unit))
                return skill + " / " + unit;
            if (!string.IsNullOrEmpty(skill))
                return skill;
            if (!string.IsNullOrEmpty(unit))
                return unit;
            return "нет";
        }

        private static bool TryGetPoint(RegionVM region, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (region.PolygonPoints == null || region.PolygonPoints.Count == 0)
                return false;

            long sx = 0;
            long sy = 0;
            var n = 0;
            PointVM first = null;
            foreach (PointVM p in region.PolygonPoints)
            {
                if (p == null)
                    continue;
                if (first == null)
                    first = p;
                sx += p.X;
                sy += p.Y;
                n++;
            }

            if (n == 0)
                return false;

            x = (int)(sx / n);
            y = (int)(sy / n);
            if (region.Contains(x, y))
                return true;

            foreach (PointVM p in region.PolygonPoints)
            {
                if (p == null)
                    continue;
                if (region.Contains(p.X, p.Y))
                {
                    x = p.X;
                    y = p.Y;
                    return true;
                }
            }

            if (first == null)
                return false;
            x = first.X;
            y = first.Y;
            return true;
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal(_headerStyle);
            GUILayout.Label("Регион", _headerStyle, GUILayout.Width(260f), GUILayout.Height(18f));
            GUILayout.Label("Dist", _headerStyle, GUILayout.Width(70f), GUILayout.Height(18f));
            GUILayout.Label("Доступ", _headerStyle, GUILayout.Width(140f), GUILayout.Height(18f));
            GUILayout.EndHorizontal();
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

            _hereStyle = new GUIStyle(_cellStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.45f) }
            };
        }

        private sealed class Row
        {
            public string Name;
            public double Dist;
            public string DistText;
            public bool Can;
            public string AccessText;
            public bool Current;
        }
    }
}
