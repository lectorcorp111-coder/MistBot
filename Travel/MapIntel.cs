using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace MistLegacyBot
{
    internal sealed class MapTile
    {
        public int x { get; set; }
        public int y { get; set; }
        public string regionId { get; set; }
        public string region { get; set; }
    }

    internal sealed class MapPathRecord
    {
        public MapTile from { get; set; }
        public MapTile to { get; set; }
    }

    internal sealed class MapCrossingRecord
    {
        public MapTile from { get; set; }
        public MapTile to { get; set; }
    }

    internal sealed class MapGatherRecord
    {
        public string resourceId { get; set; }
        public string name { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public string regionId { get; set; }
        public string region { get; set; }
    }

    internal sealed class MapPortalRecord
    {
        public string siteId { get; set; }
        public string name { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public string regionId { get; set; }
        public string region { get; set; }
    }

    internal sealed class MapSiteHopRecord
    {
        public string action { get; set; }
        public string taskId { get; set; }
        public MapSiteEnd from { get; set; }
        public MapSiteEnd to { get; set; }
    }

    internal sealed class MapPoiRecord
    {
        public string siteId { get; set; }
        public string name { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public string regionId { get; set; }
        public string region { get; set; }
        public bool portal { get; set; }
        public bool camp { get; set; }
        public bool vortex { get; set; }
    }

    internal sealed class MapSiteEnd
    {
        public string siteId { get; set; }
        public string name { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public string regionId { get; set; }
        public string region { get; set; }
    }

    internal sealed class MapIntelDoc
    {
        public int version { get; set; }
        public string updatedUtc { get; set; }
        public List<MapPathRecord> paths { get; set; }
        public List<MapCrossingRecord> crossings { get; set; }
        public List<MapGatherRecord> gathers { get; set; }
        public List<MapPortalRecord> portals { get; set; }
        public List<MapSiteHopRecord> hops { get; set; }
        public List<MapPoiRecord> pois { get; set; }

        public static MapIntelDoc Empty()
        {
            return new MapIntelDoc
            {
                version = 1,
                updatedUtc = DateTime.UtcNow.ToString("o"),
                paths = new List<MapPathRecord>(),
                crossings = new List<MapCrossingRecord>(),
                gathers = new List<MapGatherRecord>(),
                portals = new List<MapPortalRecord>(),
                hops = new List<MapSiteHopRecord>(),
                pois = new List<MapPoiRecord>()
            };
        }
    }

    internal static class MapIntelStore
    {
        public const string WorkspaceFile = @"c:\Lector\Code\MistLegacyBot\data\map-intel.json";

        public static MapIntelDoc Doc { get; private set; }

        public static string BepInExFile
        {
            get
            {
                try
                {
                    return Path.Combine(Paths.BepInExRootPath, "plugin_data", "MistLegacyBot", "map-intel.json");
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void Load()
        {
            if (Doc != null)
                return;
            Doc = Read(WorkspaceFile) ?? Read(BepInExFile) ?? MapIntelDoc.Empty();
            if (Doc.paths == null) Doc.paths = new List<MapPathRecord>();
            if (Doc.crossings == null) Doc.crossings = new List<MapCrossingRecord>();
            if (Doc.gathers == null) Doc.gathers = new List<MapGatherRecord>();
            if (Doc.portals == null) Doc.portals = new List<MapPortalRecord>();
            if (Doc.hops == null) Doc.hops = new List<MapSiteHopRecord>();
            if (Doc.pois == null) Doc.pois = new List<MapPoiRecord>();
        }

        public static bool AddPath(MapTile from, MapTile to)
        {
            Load();
            if (from == null || to == null)
                return false;
            if (from.x == to.x && from.y == to.y)
                return false;
            foreach (var p in Doc.paths)
            {
                if (SameTile(p.from, from) && SameTile(p.to, to))
                    return false;
            }

            Doc.paths.Add(new MapPathRecord { from = from, to = to });
            Save();
            return true;
        }

        public static bool AddCrossing(MapTile from, MapTile to)
        {
            Load();
            if (from == null || to == null)
                return false;
            if (from.regionId == to.regionId)
                return false;
            foreach (var c in Doc.crossings)
            {
                if (SameTile(c.from, from) && SameTile(c.to, to))
                    return false;
                if (SameTile(c.from, to) && SameTile(c.to, from))
                    return false;
            }

            Doc.crossings.Add(new MapCrossingRecord { from = from, to = to });
            Save();
            return true;
        }

        public static bool AddGather(MapGatherRecord gather)
        {
            Load();
            if (gather == null)
                return false;
            foreach (var g in Doc.gathers)
            {
                if (!string.IsNullOrEmpty(gather.resourceId) && g.resourceId == gather.resourceId)
                    return false;
                if (g.x == gather.x && g.y == gather.y && g.name == gather.name)
                    return false;
            }

            Doc.gathers.Add(gather);
            Save();
            return true;
        }

        public static bool AddPortal(MapPortalRecord portal)
        {
            Load();
            if (portal == null)
                return false;
            foreach (var p in Doc.portals)
            {
                if (!string.IsNullOrEmpty(portal.siteId) && p.siteId == portal.siteId)
                    return false;
                if (p.x == portal.x && p.y == portal.y)
                    return false;
            }

            Doc.portals.Add(portal);
            Save();
            return true;
        }

        public static bool AddHop(MapSiteHopRecord hop)
        {
            Load();
            if (hop == null || hop.from == null || hop.to == null)
                return false;
            if (hop.from.x == hop.to.x && hop.from.y == hop.to.y)
                return false;
            foreach (var h in Doc.hops)
            {
                if (SameEnd(h.from, hop.from) && SameEnd(h.to, hop.to) && h.action == hop.action)
                    return false;
            }

            Doc.hops.Add(hop);
            Save();
            return true;
        }

        public static bool AddPoi(MapPoiRecord poi)
        {
            Load();
            if (poi == null)
                return false;
            foreach (var p in Doc.pois)
            {
                if (!string.IsNullOrEmpty(poi.siteId) && p.siteId == poi.siteId)
                    return false;
                if (p.x == poi.x && p.y == poi.y && p.name == poi.name)
                    return false;
            }

            Doc.pois.Add(poi);
            Save();
            return true;
        }

        public static bool HasHopCovering(int fromX, int fromY, int toX, int toY)
        {
            Load();
            const int radius = 12;
            foreach (var h in Doc.hops)
            {
                if (h == null || h.from == null || h.to == null)
                    continue;
                if (Near(h.from, fromX, fromY, radius) && Near(h.to, toX, toY, radius))
                    return true;
                if (Near(h.from, toX, toY, radius) && Near(h.to, fromX, fromY, radius))
                    return true;
            }

            return false;
        }

        public static string Summary()
        {
            Load();
            return "путей " + Doc.paths.Count
                   + "  стыков " + Doc.crossings.Count
                   + "  сбора " + Doc.gathers.Count
                   + "  порталов " + Doc.portals.Count
                   + "  связей " + Doc.hops.Count
                   + "  точек " + Doc.pois.Count;
        }

        private static bool SameTile(MapTile a, MapTile b)
        {
            return a != null && b != null && a.x == b.x && a.y == b.y;
        }

        private static bool SameEnd(MapSiteEnd a, MapSiteEnd b)
        {
            return a != null && b != null && a.x == b.x && a.y == b.y;
        }

        private static bool Near(MapSiteEnd end, int x, int y, int radius)
        {
            var dx = end.x - x;
            var dy = end.y - y;
            return dx * dx + dy * dy <= radius * radius;
        }

        private static MapIntelDoc Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<MapIntelDoc>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        private static void Save()
        {
            Doc.version = 1;
            Doc.updatedUtc = DateTime.UtcNow.ToString("o");
            var json = JsonConvert.SerializeObject(Doc, Formatting.Indented);
            Write(WorkspaceFile, json);
            Write(BepInExFile, json);
        }

        private static void Write(string path, string json)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmp, path);
            }
            catch
            {
            }
        }
    }
}
