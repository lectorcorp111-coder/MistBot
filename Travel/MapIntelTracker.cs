using Virtys.Common.Shared.Algorithms;
using Virtys.Nebula.Client.ViewModel.Domain;

namespace MistLegacyBot
{
    internal static class MapIntelTracker
    {
        private const double ArriveDist = 12.0;

        private static bool _ready;
        private static int _prevX;
        private static int _prevY;
        private static string _prevRegionId;
        private static string _prevRegionName;

        private static bool _inTrip;
        private static MapTile _tripStart;
        private static int _tripDestX;
        private static int _tripDestY;
        private static bool _tripHasDest;
        private static bool _wasGathering;

        private static BotLogger _log;

        public static void Start(BotLogger log)
        {
            _log = log;
            MapIntelStore.Load();
            if (log != null)
                log.LogInfo("Intel: " + MapIntelStore.Summary());
        }

        public static void Tick()
        {
            var player = PlayerVM.Current;
            var group = player != null ? player.Group : null;
            var world = WorldVM.Current;
            if (group == null || world == null)
            {
                _ready = false;
                _inTrip = false;
                return;
            }

            var here = world.GetRegion(group.X, group.Y, world.Region);
            var hereId = here != null ? here.ID.ToString("D") : null;
            var hereName = here != null ? here.Name : null;

            if (_ready && !string.IsNullOrEmpty(_prevRegionId) && hereId != null && hereId != _prevRegionId)
            {
                var from = Tile(_prevX, _prevY, _prevRegionId, _prevRegionName);
                var to = Tile(group.X, group.Y, hereId, hereName);
                if (MapIntelStore.AddCrossing(from, to) && _log != null)
                    _log.LogInfo("Intel: стык «" + from.region + "» → «" + to.region + "».");
            }

            var moving = group.IsTraveling || group.TravelSearching;
            if (moving)
            {
                if (!_inTrip)
                {
                    _inTrip = true;
                    _tripStart = Tile(group.X, group.Y, hereId, hereName);
                    _tripHasDest = false;
                }

                CaptureDest(group);
            }
            else if (_inTrip)
            {
                FinishTrip(group, hereId, hereName);
                _inTrip = false;
                _tripHasDest = false;
            }

            if (group.Gathering && !_wasGathering)
                RecordGather(group, hereId, hereName);

            _wasGathering = group.Gathering;

            if (!moving)
                MaybePortal(group, hereId, hereName);

            _prevX = group.X;
            _prevY = group.Y;
            _prevRegionId = hereId;
            _prevRegionName = hereName;
            _ready = true;
        }

        private static void CaptureDest(GroupVM group)
        {
            int dx;
            int dy;
            if (!TryClick(group, out dx, out dy) || _tripStart == null)
                return;

            if (!_tripHasDest)
            {
                _tripDestX = dx;
                _tripDestY = dy;
                _tripHasDest = true;
                return;
            }

            var newer = Distance.Measure(dx, dy, _tripStart.x, _tripStart.y);
            var older = Distance.Measure(_tripDestX, _tripDestY, _tripStart.x, _tripStart.y);
            if (newer >= older)
            {
                _tripDestX = dx;
                _tripDestY = dy;
            }
        }

        private static void FinishTrip(GroupVM group, string hereId, string hereName)
        {
            if (_tripStart == null)
                return;

            var arrived = _tripHasDest
                && Distance.Measure(_tripDestX, _tripDestY, group.X, group.Y) <= ArriveDist;
            if (!arrived && group.Gathering)
                arrived = true;
            if (!arrived)
            {
                if (_log != null && _tripHasDest)
                {
                    _log.LogInfo(
                        "Intel: путь не засчитан, dist="
                        + Distance.Measure(_tripDestX, _tripDestY, group.X, group.Y).ToString("F1")
                        + " до (" + _tripDestX + "," + _tripDestY + ").");
                }
                return;
            }

            var to = Tile(group.X, group.Y, hereId, hereName);
            if (MapIntelStore.AddPath(_tripStart, to) && _log != null)
                _log.LogInfo("Intel: путь (" + _tripStart.x + "," + _tripStart.y + ") → (" + to.x + "," + to.y + ").");

            RecordGather(group, hereId, hereName);
            MaybePortal(group, hereId, hereName);
        }

        private static void RecordGather(GroupVM group, string hereId, string hereName)
        {
            var resource = FindResourceAt(group, group.X, group.Y);
            if (resource == null && _tripHasDest)
                resource = FindResourceAt(group, _tripDestX, _tripDestY);
            if (resource == null)
                resource = FindGatheringResource(group);
            if (resource == null)
                return;

            var region = resource.RegionResource != null ? resource.RegionResource.Region : null;
            var gather = new MapGatherRecord
            {
                resourceId = resource.RegionResource != null ? resource.RegionResource.ID.ToString("D") : "",
                name = resource.ItemBase != null ? resource.ItemBase.Name : "?",
                x = resource.X,
                y = resource.Y,
                regionId = region != null ? region.ID.ToString("D") : hereId,
                region = region != null ? region.Name : hereName
            };
            if (MapIntelStore.AddGather(gather) && _log != null)
                _log.LogInfo("Intel: сбор «" + gather.name + "» " + gather.resourceId + ".");
        }

        private static ItemResourceVM FindGatheringResource(GroupVM group)
        {
            var player = group.Player != null ? group.Player : PlayerVM.Current;
            if (player == null || player.Resources == null)
                return null;
            foreach (var item in player.Resources)
            {
                if (item != null && item.GatheringQuantity > 0)
                    return item;
            }

            return null;
        }

        private static void MaybePortal(GroupVM group, string hereId, string hereName)
        {
            var site = group.Destination != null ? group.Destination.Site : null;
            if (site == null || !site.Portal)
            {
                site = FindPortalSite(group);
                if (site == null)
                    return;
            }

            var range = site.Range > 0 ? site.Range : 2;
            if (Distance.Measure(site.X, site.Y, group.X, group.Y) > range + 1)
                return;

            var region = site.Region;
            var portal = new MapPortalRecord
            {
                siteId = site.ID.ToString("D"),
                name = string.IsNullOrEmpty(site.Name) ? "портал" : site.Name,
                x = site.X,
                y = site.Y,
                regionId = region != null ? region.ID.ToString("D") : hereId,
                region = region != null ? region.Name : hereName
            };
            if (MapIntelStore.AddPortal(portal) && _log != null)
                _log.LogInfo("Intel: портал «" + portal.name + "».");
        }

        private static SiteVM FindPortalSite(GroupVM group)
        {
            var world = WorldVM.Current;
            if (world == null || world.Sites == null)
                return null;
            SiteVM best = null;
            var bestDist = double.MaxValue;
            foreach (var site in world.Sites)
            {
                if (site == null || !site.Portal || !site.OnMap || site.Unknown)
                    continue;
                var d = Distance.Measure(site.X, site.Y, group.X, group.Y);
                var range = site.Range > 0 ? site.Range : 2;
                if (d <= range + 1 && d < bestDist)
                {
                    bestDist = d;
                    best = site;
                }
            }

            return best;
        }

        private static ItemResourceVM FindResourceAt(GroupVM group, int x, int y)
        {
            var player = group.Player != null ? group.Player : PlayerVM.Current;
            if (player == null || player.Resources == null)
                return null;

            ItemResourceVM best = null;
            var bestDist = double.MaxValue;
            foreach (var item in player.Resources)
            {
                if (item == null || item.ItemBase == null)
                    continue;
                var d = Distance.Measure(item.X, item.Y, x, y);
                if (d <= ArriveDist && d < bestDist)
                {
                    bestDist = d;
                    best = item;
                }
            }

            return best;
        }

        private static bool TryClick(GroupVM group, out int x, out int y)
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

        private static MapTile Tile(int x, int y, string regionId, string region)
        {
            return new MapTile
            {
                x = x,
                y = y,
                regionId = regionId,
                region = region
            };
        }
    }
}
