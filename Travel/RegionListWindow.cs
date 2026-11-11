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

        private Rect _window = new Rect(580f, 490f, 520f, 490f);
        private Vector2 _scroll;
        private int _sortIndex;
        private GUIStyle _headerStyle;
        private GUIStyle _cellStyle;
        private GUIStyle _hereStyle;
        private GUIStyle _statusStyle;

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
            DrawMovement(group, world, here);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawMovement(GroupVM group, WorldVM world, RegionVM here)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Движение", _headerStyle);
            GUILayout.Label("Intel  " + MapIntelStore.Summary(), _statusStyle);

            var dest = group.Destination;
            var destRegion = dest != null ? dest.Region : null;
            var destSite = dest != null ? dest.Site : null;

            int clickX;
            int clickY;
            var hasClick = TryGetClick(group, out clickX, out clickY);
            var clickRegion = hasClick ? world.GetRegion(clickX, clickY, destRegion) : destRegion;

            GUILayout.Label(
                "Сейчас  " + (here != null ? here.Name : "?")
                + "  (" + group.X + "," + group.Y + ")"
                + (group.IsTraveling ? "  ~(" + group.FloatX.ToString("F1") + "," + group.FloatY.ToString("F1") + ")" : ""),
                _statusStyle);

            GUILayout.Label("Состояние  " + FormatState(group, destSite), _statusStyle);

            var gather = FormatGather(group);
            if (gather != null)
                GUILayout.Label("Сбор  " + gather, _statusStyle);

            if (hasClick)
            {
                var clickDist = Distance.Measure(clickX, clickY, group.X, group.Y);
                GUILayout.Label(
                    "Клик  (" + clickX + "," + clickY + ")"
                    + (clickRegion != null ? "  " + clickRegion.Name : "")
                    + "  dist " + clickDist.ToString("F0"),
                    _statusStyle);
            }
            else
            {
                GUILayout.Label("Клик  —", _statusStyle);
            }

            var going = destRegion != null ? destRegion.Name : (clickRegion != null ? clickRegion.Name : "—");
            var pathCount = group.TravelLocations != null ? group.TravelLocations.Count : 0;
            var next = FormatNextPoint(group);
            GUILayout.Label(
                "Идёт в  " + going
                + (pathCount > 0 ? "  точек " + pathCount : "")
                + (next != null ? "  след " + next : ""),
                _statusStyle);

            if (destSite != null && !string.IsNullOrEmpty(destSite.Name))
                GUILayout.Label("Точка  " + destSite.Name, _statusStyle);

            var msg = group.TravelMessage != null ? group.TravelMessage.Message : null;
            if (!string.IsNullOrEmpty(msg))
                GUILayout.Label("Сообщение  " + msg, _statusStyle);
        }

        private static string FormatState(GroupVM group, SiteVM destSite)
        {
            if (group.Combat != null)
                return "бой";
            if (group.Gathering)
                return "сбор";
            if (group.TravelSearching)
                return "поиск пути";
            if (group.IsTraveling)
                return "идёт";
            if (group.IsWaiting)
                return "ждёт";
            if (group.Deployed)
                return destSite != null && !string.IsNullOrEmpty(destSite.Name)
                    ? "на точке «" + destSite.Name + "»"
                    : "на точке";
            if (group.BlockTravel)
                return "ход заблокирован";
            if (group.IsBusy)
                return "занят";
            return "стоит";
        }

        private static string FormatGather(GroupVM group)
        {
            var resource = FindGatheringResource(group);
            var task = FormatCurrentTask(group);
            if (resource == null && task == null && !group.Gathering)
                return null;

            if (resource == null)
                return group.Gathering ? (task ?? "идёт") : task;

            var name = resource.ItemBase != null ? resource.ItemBase.Name : "?";
            var qty = resource.GatheringQuantity > 0 ? " x" + resource.GatheringQuantity : "";
            var left = resource.Remaining > TimeSpan.Zero ? "  ещё " + FormatSpan(resource.Remaining) : "";
            var until = group.DueDateTime != default(DateTime) && group.Gathering
                ? "  до " + group.DueDateTime.ToString("HH:mm:ss")
                : "";
            var where = " @ (" + resource.X + "," + resource.Y + ")";
            var extra = task != null ? "  [" + task + "]" : "";
            return name + qty + where + left + until + extra;
        }

        private static ItemResourceVM FindGatheringResource(GroupVM group)
        {
            var player = group.Player != null ? group.Player : PlayerVM.Current;
            if (player == null || player.Resources == null)
                return null;

            ItemResourceVM nearby = null;
            var nearbyDist = double.MaxValue;
            foreach (var item in player.Resources)
            {
                if (item == null)
                    continue;
                if (item.GatheringQuantity > 0)
                    return item;
                if (!group.Gathering)
                    continue;
                var d = Distance.Measure(item.X, item.Y, group.X, group.Y);
                if (d <= 2.0 && d < nearbyDist)
                {
                    nearbyDist = d;
                    nearby = item;
                }
            }

            return nearby;
        }

        private static string FormatCurrentTask(GroupVM group)
        {
            var current = group.CurrentTask;
            if (current == null)
                return null;
            if (current.Item != null && !string.IsNullOrEmpty(current.Item.Name))
                return current.Item.Name;
            if (current.TaskObject != null && !string.IsNullOrEmpty(current.TaskObject.Name))
                return current.TaskObject.Name;
            var gt = current.GroupTask;
            if (gt != null && !string.IsNullOrEmpty(gt.Name))
                return gt.Name;
            return null;
        }

        private static string FormatSpan(TimeSpan span)
        {
            if (span.TotalHours >= 1)
                return span.ToString(@"h\:mm\:ss");
            return span.ToString(@"mm\:ss");
        }

        private static bool TryGetClick(GroupVM group, out int x, out int y)
        {
            x = 0;
            y = 0;
            var points = group.TravelLocations;
            if (points != null && points.Count > 0)
            {
                var last = points[points.Count - 1];
                if (last == null)
                    return false;
                x = last.X;
                y = last.Y;
                return true;
            }

            var dest = group.Destination;
            if (dest != null && dest.Site != null)
            {
                x = dest.Site.X;
                y = dest.Site.Y;
                return true;
            }

            return false;
        }

        private static string FormatNextPoint(GroupVM group)
        {
            var points = group.TravelLocations;
            if (points == null || points.Count == 0)
                return null;
            var p = points[0];
            if (p == null)
                return null;
            return "(" + p.X + "," + p.Y + ")";
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

            _statusStyle = new GUIStyle(_cellStyle)
            {
                wordWrap = true
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
