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

            if (snap.Types.Count == 0)
            {
                log.LogWarning("Stats: список ресурсов пуст.");
                return;
            }

            foreach (var t in snap.Types)
            {
                var dist = t.NearestDistance.HasValue ? t.NearestDistance.Value.ToString("F1") : "-";
                log.LogInfo(
                    $"  {t.Name}: узлов {t.NodeCount}, вид {t.VisibleCount}, gather {t.CanGatherCount}, qty {t.QuantitySum}, coffer {t.CofferSum}, ближ {dist}");
            }
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
