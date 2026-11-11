using System;
using Virtys.Common.Shared.Algorithms;
using Virtys.Nebula.Client.ViewModel.Domain;

namespace MistLegacyBot
{
    internal static class MapIntelTracker
    {
        private const double ArriveDist = 12.0;
        private const double HopJumpDist = 6.0;

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
        private static MapSiteEnd _pendingFrom;
        private static string _pendingAction;
        private static string _pendingTaskId;
        private static int _pendingToX;
        private static int _pendingToY;

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

            WatchHopTask(group, hereId, hereName);
            var jumped = _ready && Distance.Measure(_prevX, _prevY, group.X, group.Y) >= HopJumpDist;
            if (jumped)
                RecordJump(group, hereId, hereName);

            if (_ready && !string.IsNullOrEmpty(_prevRegionId) && hereId != null && hereId != _prevRegionId)
            {
                if (!MapIntelStore.HasHopCovering(_prevX, _prevY, group.X, group.Y))
                {
                    var from = Tile(_prevX, _prevY, _prevRegionId, _prevRegionName);
                    var to = Tile(group.X, group.Y, hereId, hereName);
                    if (MapIntelStore.AddCrossing(from, to) && _log != null)
                        _log.LogInfo("Intel: стык «" + from.region + "» → «" + to.region + "».");
                }
            }

            if (!moving)
            {
                MaybePortal(group, hereId, hereName);
                ScanSiteHops(group, hereId, hereName);
                RecordPoi(group, hereId, hereName);
            }

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

            var to = Tile(group.X, group.Y, hereId, hereName);
            if (MapIntelStore.AddPath(_tripStart, to) && _log != null)
                _log.LogInfo("Intel: путь (" + _tripStart.x + "," + _tripStart.y + ") → (" + to.x + "," + to.y + ").");

            RecordGather(group, hereId, hereName);
            MaybePortal(group, hereId, hereName);
            RecordPoi(group, hereId, hereName);
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

        private static void RecordPoi(GroupVM group, string hereId, string hereName)
        {
            var site = group.Destination != null ? group.Destination.Site : null;
            if (site == null || site.Unknown || string.IsNullOrEmpty(site.Name))
                site = NearestNamedSite(group.X, group.Y);
            if (site == null)
                return;

            var range = site.Range > 0 ? site.Range : 2;
            if (Distance.Measure(site.X, site.Y, group.X, group.Y) > range + 2)
                return;

            var region = site.Region;
            var poi = new MapPoiRecord
            {
                siteId = site.ID.ToString("D"),
                name = site.Name,
                x = site.X,
                y = site.Y,
                regionId = region != null ? region.ID.ToString("D") : hereId,
                region = region != null ? region.Name : hereName,
                portal = site.Portal,
                camp = site.IsCamp,
                vortex = site.Vortex
            };
            if (MapIntelStore.AddPoi(poi) && _log != null)
                _log.LogInfo("Intel: точка «" + poi.name + "» (" + poi.x + "," + poi.y + ").");
        }

        private static SiteVM NearestNamedSite(int x, int y)
        {
            var world = WorldVM.Current;
            if (world == null || world.Sites == null)
                return null;
            SiteVM best = null;
            var bestDist = ArriveDist;
            foreach (var site in world.Sites)
            {
                if (site == null || site.Unknown || string.IsNullOrEmpty(site.Name))
                    continue;
                var d = Distance.Measure(site.X, site.Y, x, y);
                var range = site.Range > 0 ? site.Range : 2;
                if (d <= range + 2 && d < bestDist)
                {
                    bestDist = d;
                    best = site;
                }
            }

            return best;
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

        private static void WatchHopTask(GroupVM group, string hereId, string hereName)
        {
            var task = HopTaskOf(group);
            if (task == null)
                return;

            int toX;
            int toY;
            if (!TryTeleportCoords(task, out toX, out toY))
                return;

            var fromSite = NearestSite(group.X, group.Y, false);
            _pendingFrom = SiteEnd(fromSite, group.X, group.Y, hereId, hereName);
            _pendingAction = string.IsNullOrEmpty(task.Name) ? "переход" : task.Name;
            _pendingTaskId = task.ID.ToString("D");
            _pendingToX = toX;
            _pendingToY = toY;
        }

        private static void RecordJump(GroupVM group, string hereId, string hereName)
        {
            var fromSite = NearestSite(_prevX, _prevY, false);
            var toSite = NearestSite(group.X, group.Y, false);
            var from = _pendingFrom ?? SiteEnd(fromSite, _prevX, _prevY, _prevRegionId, _prevRegionName);
            var to = SiteEnd(toSite, group.X, group.Y, hereId, hereName);
            var action = _pendingAction;
            var taskId = _pendingTaskId;
            if (string.IsNullOrEmpty(action))
            {
                var task = HopTaskOf(group);
                if (task != null)
                {
                    action = task.Name;
                    taskId = task.ID.ToString("D");
                }
            }

            if (fromSite != null && fromSite.Portal)
                return;
            if (toSite != null && toSite.Portal)
                return;

            TryAddHop(from, to, action, taskId);
            RecordPoi(group, hereId, hereName);
            _pendingFrom = null;
            _pendingAction = null;
            _pendingTaskId = null;
        }

        private static void ScanSiteHops(GroupVM group, string hereId, string hereName)
        {
            var world = WorldVM.Current;
            if (world == null || world.Sites == null)
                return;

            foreach (var site in world.Sites)
            {
                if (site == null || site.Portal || site.Unknown || site.Tasks == null)
                    continue;
                var range = site.Range > 0 ? site.Range : 2;
                if (Distance.Measure(site.X, site.Y, group.X, group.Y) > range + 2)
                    continue;

                foreach (var task in site.Tasks)
                {
                    int toX;
                    int toY;
                    if (!IsSiteHop(task) || !TryTeleportCoords(task, out toX, out toY))
                        continue;
                    var destSite = NearestSite(toX, toY, false);
                    var from = SiteEnd(site, site.X, site.Y, hereId, hereName);
                    var region = site.Region;
                    if (region != null)
                    {
                        from.regionId = region.ID.ToString("D");
                        from.region = region.Name;
                    }

                    var to = SiteEnd(destSite, toX, toY, hereId, hereName);
                    TryAddHop(from, to, string.IsNullOrEmpty(task.Name) ? "переход" : task.Name, task.ID.ToString("D"));
                }
            }
        }

        private static void TryAddHop(MapSiteEnd from, MapSiteEnd to, string action, string taskId)
        {
            var hop = new MapSiteHopRecord
            {
                action = action,
                taskId = taskId,
                from = from,
                to = to
            };
            if (MapIntelStore.AddHop(hop) && _log != null)
            {
                var name = from != null && !string.IsNullOrEmpty(from.name) ? from.name : "?";
                _log.LogInfo("Intel: связь «" + (action ?? "переход") + "» «" + name + "» ("
                             + from.x + "," + from.y + ") → (" + to.x + "," + to.y + ").");
            }
        }

        private static TaskVM HopTaskOf(GroupVM group)
        {
            if (group == null)
                return null;
            var current = group.CurrentTask;
            if (current != null && current.GroupTask != null && IsSiteHop(current.GroupTask.Task))
                return current.GroupTask.Task;
            if (IsSiteHop(group.TransitioningAction))
                return group.TransitioningAction;
            return null;
        }

        private static bool IsSiteHop(TaskVM task)
        {
            return task != null && task.IsTeleport && !task.HomePortal;
        }

        private static bool TryTeleportCoords(TaskVM task, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (task == null)
                return false;
            var tx = task.TeleportX;
            var ty = task.TeleportY;
            if (!tx.HasValue || !ty.HasValue)
                return false;
            x = tx.Value;
            y = ty.Value;
            return true;
        }

        private static SiteVM NearestSite(int x, int y, bool portals)
        {
            var world = WorldVM.Current;
            if (world == null || world.Sites == null)
                return null;
            SiteVM best = null;
            var bestDist = ArriveDist;
            foreach (var site in world.Sites)
            {
                if (site == null || site.Unknown)
                    continue;
                if (site.Portal != portals)
                    continue;
                var d = Distance.Measure(site.X, site.Y, x, y);
                var range = site.Range > 0 ? site.Range : 2;
                if (d <= range + 2 && d < bestDist)
                {
                    bestDist = d;
                    best = site;
                }
            }

            return best;
        }

        private static MapSiteEnd SiteEnd(SiteVM site, int x, int y, string regionId, string region)
        {
            if (site != null)
            {
                x = site.X;
                y = site.Y;
                var r = site.Region;
                if (r != null)
                {
                    regionId = r.ID.ToString("D");
                    region = r.Name;
                }
            }

            return new MapSiteEnd
            {
                siteId = site != null ? site.ID.ToString("D") : null,
                name = site != null ? site.Name : null,
                x = x,
                y = y,
                regionId = regionId,
                region = region
            };
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
