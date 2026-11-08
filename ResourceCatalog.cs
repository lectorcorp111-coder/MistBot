using System;
using System.Collections.Generic;
using System.Linq;
using Virtys.Common.Shared.Algorithms;
using Virtys.Nebula.Client.ViewModel.Domain;

namespace MistLegacyBot
{
    internal sealed class ResourceCatalog
    {
        public static ResourceCatalog Instance { get; } = new ResourceCatalog();

        private readonly List<ResourceRecord> _nodes = new List<ResourceRecord>();

        public IReadOnlyList<ResourceRecord> Nodes
        {
            get { return _nodes; }
        }

        public bool Ok { get; private set; }
        public string Error { get; private set; }
        public int Unknown { get; private set; }
        public int BagFree { get; private set; }
        public int BagCapacity { get; private set; }

        public int VisibleCount
        {
            get { return _nodes.Count(n => n.Visible && n.OnMap && n.Quantity.GetValueOrDefault() > 0); }
        }

        public int CanGatherCount
        {
            get { return _nodes.Count(n => n.CanGather); }
        }

        public int LiveCount
        {
            get { return _nodes.Count(n => n.Quantity != null); }
        }

        public void Refresh()
        {
            _nodes.Clear();
            Ok = false;
            Error = null;
            Unknown = 0;
            BagFree = 0;
            BagCapacity = 0;

            var player = PlayerVM.Current;
            if (player == null)
            {
                Error = "Персонаж не в мире.";
                return;
            }

            var group = player.Group;
            if (group == null)
            {
                Error = "Нет отряда на карте.";
                return;
            }

            Unknown = group.UnknownResources != null ? group.UnknownResources.Count : 0;
            if (group.ItemContainer != null)
            {
                BagFree = group.ItemContainer.FreeSpace;
                BagCapacity = group.ItemContainer.Capacity;
            }

            var liveById = new Dictionary<Guid, ItemResourceVM>();
            if (player.Resources != null)
            {
                foreach (var item in player.Resources)
                {
                    if (item == null || item.RegionResource == null)
                        continue;
                    liveById[item.RegionResource.ID] = item;
                }
            }

            var gx = group.X;
            var gy = group.Y;
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
                        _nodes.Add(ToRecord(node.ItemBase, region.Name, node, live, gx, gy));
                    }
                }
            }

            if (_nodes.Count == 0 && player.Resources != null)
            {
                foreach (var item in player.Resources)
                {
                    if (item == null)
                        continue;
                    var regionName = item.RegionResource != null && item.RegionResource.Region != null
                        ? item.RegionResource.Region.Name
                        : "?";
                    _nodes.Add(ToRecord(item.ItemBase, regionName, item.RegionResource, item, gx, gy));
                }
            }

            Ok = true;
        }

        public IEnumerable<IGrouping<string, ResourceRecord>> Grouped(ResourceGroupMode mode)
        {
            IEnumerable<ResourceRecord> ordered = _nodes.OrderBy(n => n.Name).ThenBy(n => n.Region);
            if (mode == ResourceGroupMode.None)
                return ordered.GroupBy(n => string.Empty);

            return ordered
                .GroupBy(n => n.GroupKey(mode))
                .OrderBy(g => g.Key);
        }

        private static ResourceRecord ToRecord(
            ItemBaseVM itemBase,
            string regionName,
            RegionResourceVM template,
            ItemResourceVM live,
            int groupX,
            int groupY)
        {
            var skill = itemBase != null ? itemBase.HarvestSkill : null;
            double? dist = null;
            if (live != null)
                dist = Distance.Measure(live.X, live.Y, groupX, groupY);

            return new ResourceRecord
            {
                Id = template != null ? template.ID : Guid.Empty,
                Name = itemBase != null ? itemBase.Name : "?",
                Rarity = itemBase != null ? itemBase.Rarity.ToString() : "?",
                Level = itemBase != null ? itemBase.Level : 0,
                Region = string.IsNullOrEmpty(regionName) ? "?" : regionName,
                Skill = skill != null ? skill.Name : "-",
                SkillDifficulty = itemBase != null ? Convert.ToString(itemBase.HarvestDifficulty) : "",
                Quantity = live != null ? live.Quantity : (int?)null,
                Cooldown = FormatCooldown(template, live),
                Visible = live != null && live.Visible,
                OnMap = live != null && live.OnMap,
                CanGather = live != null && live.CanGather,
                X = live != null ? live.X : (template != null && template.X.HasValue ? template.X.Value : 0),
                Y = live != null ? live.Y : (template != null && template.Y.HasValue ? template.Y.Value : 0),
                Distance = dist
            };
        }

        private static string FormatCooldown(RegionResourceVM template, ItemResourceVM live)
        {
            var templateCd = template != null ? FormatTime(template.NodeCooldown) : "?";
            if (live == null)
                return "шаблон " + templateCd;
            if (live.Quantity > 0)
                return "готов (" + templateCd + ")";
            if (live.Remaining > TimeSpan.Zero)
                return FormatTime(live.Remaining) + " / " + templateCd;
            if (!string.IsNullOrEmpty(live.Cooldown))
                return live.Cooldown + " / " + templateCd;
            if (live.PlayerResource != null && live.PlayerResource.DueDateTime != default(DateTime))
                return "до " + live.PlayerResource.DueDateTime.ToString("HH:mm:ss") + " / " + templateCd;
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
}
