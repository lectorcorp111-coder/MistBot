using System;
using System.Collections.Generic;
using System.Linq;
using Virtys.Common.Shared.Algorithms;
using Virtys.Nebula.Client.ViewModel.Domain;

namespace MistLegacyBot
{
    internal static class ResourceStatsService
    {
        public static ResourceStatsSnapshot Capture()
        {
            var player = PlayerVM.Current;
            if (player == null)
                return ResourceStatsSnapshot.NotInWorld();

            var group = player.Group;
            if (group == null)
                return ResourceStatsSnapshot.NoGroup();

            var list = player.Resources;
            var unknown = group.UnknownResources;
            var gx = group.X;
            var gy = group.Y;

            var nodes = new List<ResourceNodeStat>();
            if (list != null)
            {
                foreach (var r in list)
                {
                    if (r == null)
                        continue;

                    var dist = Distance.Measure(r.X, r.Y, gx, gy);
                    nodes.Add(new ResourceNodeStat(
                        r.ItemBase != null ? r.ItemBase.Name : "?",
                        r.X,
                        r.Y,
                        r.Quantity,
                        r.QuantityInCoffer,
                        dist,
                        r.Visible,
                        r.OnMap,
                        r.CanGather,
                        r.HasSkillToGather,
                        r.ErrorMessage));
                }
            }

            var byName = nodes
                .GroupBy(n => n.Name)
                .Select(g => new ResourceTypeStat(
                    g.Key,
                    g.Count(),
                    g.Count(n => n.Visible && n.OnMap && n.Quantity > 0),
                    g.Count(n => n.CanGather),
                    g.Sum(n => n.Quantity),
                    g.Sum(n => n.QuantityInCoffer),
                    g.Where(n => n.Visible && n.OnMap && n.Quantity > 0)
                        .Select(n => (double?)n.Distance)
                        .DefaultIfEmpty(null)
                        .Min()))
                .OrderByDescending(t => t.CanGatherCount)
                .ThenByDescending(t => t.VisibleCount)
                .ThenBy(t => t.Name)
                .ToList();

            var bagFree = group.ItemContainer != null ? group.ItemContainer.FreeSpace : 0;
            var bagCap = group.ItemContainer != null ? group.ItemContainer.Capacity : 0;

            return new ResourceStatsSnapshot(
                ok: true,
                error: null,
                total: nodes.Count,
                unknown: unknown != null ? unknown.Count : 0,
                visible: nodes.Count(n => n.Visible && n.OnMap && n.Quantity > 0),
                canGather: nodes.Count(n => n.CanGather),
                bagFree: bagFree,
                bagCapacity: bagCap,
                types: byName);
        }

        public static void DumpToLog(BotLogger log)
        {
            var snap = Capture();
            if (!snap.Ok)
            {
                log.LogWarning(snap.Error);
                return;
            }

            log.LogInfo(
                $"Stats: узлов {snap.Total}, видимых {snap.Visible}, можно собрать {snap.CanGather}, туман {snap.Unknown}, сумка {snap.BagFree}/{snap.BagCapacity}.");

            var lines = BuildDetailLines();
            if (lines.Count == 0)
            {
                log.LogWarning("Stats: нет каталога ресурсов (WorldVM / Player.Resources).");
                return;
            }

            log.LogInfo($"Stats: {lines.Count} записей (имя | тир | регион | навык | откат).");
            foreach (var line in lines)
                log.LogInfo("  " + line);
        }

        private static List<string> BuildDetailLines()
        {
            var player = PlayerVM.Current;
            var liveById = new Dictionary<Guid, ItemResourceVM>();
            if (player != null && player.Resources != null)
            {
                foreach (var item in player.Resources)
                {
                    if (item == null || item.RegionResource == null)
                        continue;
                    liveById[item.RegionResource.ID] = item;
                }
            }

            var lines = new List<string>();
            var world = WorldVM.Current;
            if (world != null && world.Regions != null)
            {
                foreach (var region in world.Regions)
                {
                    if (region == null || region.Resources == null)
                        continue;
                    foreach (var node in region.Resources)
                    {
                        if (node == null)
                            continue;
                        ItemResourceVM live;
                        liveById.TryGetValue(node.ID, out live);
                        lines.Add(FormatDetail(node.ItemBase, region.Name, node, live));
                    }
                }
            }

            if (lines.Count > 0)
                return lines;

            if (player == null || player.Resources == null)
                return lines;

            foreach (var item in player.Resources)
            {
                if (item == null)
                    continue;
                var regionName = item.RegionResource != null && item.RegionResource.Region != null
                    ? item.RegionResource.Region.Name
                    : "?";
                lines.Add(FormatDetail(item.ItemBase, regionName, item.RegionResource, item));
            }

            return lines;
        }

        private static string FormatDetail(
            ItemBaseVM itemBase,
            string regionName,
            RegionResourceVM template,
            ItemResourceVM live)
        {
            var name = itemBase != null ? itemBase.Name : "?";
            var tier = FormatTier(itemBase);
            var skill = FormatSkill(itemBase);
            var cooldown = FormatCooldown(template, live);
            var region = string.IsNullOrEmpty(regionName) ? "?" : regionName;
            var qty = live != null ? live.Quantity.ToString() : "-";
            var vis = live == null ? "нет в live" : (live.Visible && live.OnMap ? "вид" : "скрыт");
            return $"{name} | {tier} | {region} | {skill} | qty {qty} | {cooldown} | {vis}";
        }

        private static string FormatTier(ItemBaseVM itemBase)
        {
            if (itemBase == null)
                return "?";
            return $"{itemBase.Rarity} L{itemBase.Level}";
        }

        private static string FormatSkill(ItemBaseVM itemBase)
        {
            if (itemBase == null)
                return "?";
            var skill = itemBase.HarvestSkill;
            var skillName = skill != null ? skill.Name : "-";
            return $"{skillName} {itemBase.HarvestDifficulty}";
        }

        private static string FormatCooldown(RegionResourceVM template, ItemResourceVM live)
        {
            var templateCd = template != null ? FormatTime(template.NodeCooldown) : "?";
            if (live == null)
                return "шаблон " + templateCd;
            if (live.Quantity > 0)
                return "готов (шаблон " + templateCd + ")";
            if (live.Remaining > TimeSpan.Zero)
                return FormatTime(live.Remaining) + " / шаблон " + templateCd;
            if (!string.IsNullOrEmpty(live.Cooldown))
                return live.Cooldown + " / шаблон " + templateCd;
            if (live.PlayerResource != null && live.PlayerResource.DueDateTime != default(DateTime))
                return "до " + live.PlayerResource.DueDateTime.ToString("HH:mm:ss") + " / шаблон " + templateCd;
            return "шаблон " + templateCd;
        }

        private static string FormatTime(TimeSpan span)
        {
            if (span <= TimeSpan.Zero)
                return "0";
            if (span.TotalHours >= 1)
                return span.ToString(@"h\:mm\:ss");
            return span.ToString(@"mm\:ss");
        }
    }

    internal sealed class ResourceStatsSnapshot
    {
        public ResourceStatsSnapshot(
            bool ok,
            string error,
            int total,
            int unknown,
            int visible,
            int canGather,
            int bagFree,
            int bagCapacity,
            IReadOnlyList<ResourceTypeStat> types)
        {
            Ok = ok;
            Error = error;
            Total = total;
            Unknown = unknown;
            Visible = visible;
            CanGather = canGather;
            BagFree = bagFree;
            BagCapacity = bagCapacity;
            Types = types ?? new List<ResourceTypeStat>();
        }

        public bool Ok { get; }
        public string Error { get; }
        public int Total { get; }
        public int Unknown { get; }
        public int Visible { get; }
        public int CanGather { get; }
        public int BagFree { get; }
        public int BagCapacity { get; }
        public IReadOnlyList<ResourceTypeStat> Types { get; }

        public static ResourceStatsSnapshot NotInWorld()
        {
            return new ResourceStatsSnapshot(false, "Stats: персонаж не в мире.", 0, 0, 0, 0, 0, 0, null);
        }

        public static ResourceStatsSnapshot NoGroup()
        {
            return new ResourceStatsSnapshot(false, "Stats: нет отряда на карте.", 0, 0, 0, 0, 0, 0, null);
        }
    }

    internal sealed class ResourceTypeStat
    {
        public ResourceTypeStat(
            string name,
            int nodeCount,
            int visibleCount,
            int canGatherCount,
            int quantitySum,
            int cofferSum,
            double? nearestDistance)
        {
            Name = name;
            NodeCount = nodeCount;
            VisibleCount = visibleCount;
            CanGatherCount = canGatherCount;
            QuantitySum = quantitySum;
            CofferSum = cofferSum;
            NearestDistance = nearestDistance;
        }

        public string Name { get; }
        public int NodeCount { get; }
        public int VisibleCount { get; }
        public int CanGatherCount { get; }
        public int QuantitySum { get; }
        public int CofferSum { get; }
        public double? NearestDistance { get; }
    }

    internal sealed class ResourceNodeStat
    {
        public ResourceNodeStat(
            string name,
            int x,
            int y,
            int quantity,
            int quantityInCoffer,
            double distance,
            bool visible,
            bool onMap,
            bool canGather,
            bool hasSkill,
            string errorMessage)
        {
            Name = name;
            X = x;
            Y = y;
            Quantity = quantity;
            QuantityInCoffer = quantityInCoffer;
            Distance = distance;
            Visible = visible;
            OnMap = onMap;
            CanGather = canGather;
            HasSkill = hasSkill;
            ErrorMessage = errorMessage;
        }

        public string Name { get; }
        public int X { get; }
        public int Y { get; }
        public int Quantity { get; }
        public int QuantityInCoffer { get; }
        public double Distance { get; }
        public bool Visible { get; }
        public bool OnMap { get; }
        public bool CanGather { get; }
        public bool HasSkill { get; }
        public string ErrorMessage { get; }
    }
}
