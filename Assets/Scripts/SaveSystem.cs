using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int version = 1;
    public int seed;
    public int latitude, longitude;
    public int currentTurn;
    public List<TileSave> tiles = new List<TileSave>();
    public List<RoadSave> roads = new List<RoadSave>();
    public List<SettlementSave> settlements = new List<SettlementSave>();
    public List<SettlerSave> settlers = new List<SettlerSave>();
    public List<ProjectSave> projects = new List<ProjectSave>();
}

[Serializable] public class PileSave { public string resource; public int amount; }

[Serializable] public class TileSave { public int x, z; public string biome, resource; public float height; }

[Serializable] public class RoadSave { public int ax, az, bx, bz, tier; }

[Serializable] public class ProcessorSave { public string building; public int tier; }

[Serializable]
public class SettlementSave
{
    public int x, z, tier, population;
    public bool isCapital, hasProducer;
    public List<PileSave> resources;
    public List<ProcessorSave> processors = new List<ProcessorSave>();
}

[Serializable]
public class SettlerSave
{
    public int turnsLeft, totalTurns, settlers;
    public List<PileSave> resources;
    // Tile indices (x * longitude + z) from origin to destination
    public List<int> route;
}

[Serializable]
public class ProjectSave
{
    public string name, building;
    public ProjectType type;
    public int ownerX, ownerZ, turnsLeft, totalTurns, targetTier;
    public int destX = -1, destZ = -1;
    public RoadTier roadTier;
    public List<PileSave> cargo;
    public List<int> route;
}

/**
 * Writes the whole game to a single JSON save file and reads it back.
 * Loading sets PendingLoad and reloads the game scene, which rebuilds from it.
 */
public static class SaveSystem
{
    public static SaveData PendingLoad;

    public static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, "save.json");
    public static bool HasSave => File.Exists(FilePath);

    private static Dictionary<string, ResourceData> resourceLookup;

    // ResourceData assets live under Assets/Resources, so they can be found by name at runtime
    public static ResourceData FindResource(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (resourceLookup == null)
        {
            resourceLookup = new Dictionary<string, ResourceData>();
            foreach (var r in Resources.LoadAll<ResourceData>("")) resourceLookup[r.name] = r;
        }
        return resourceLookup.TryGetValue(name, out var found) ? found : null;
    }

    public static void Write(SaveData data) => File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));

    public static SaveData Read()
    {
        try
        {
            return HasSave ? JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not read save: " + e.Message);
            return null;
        }
    }

    public static List<PileSave> Piles(ResourceCollection collection)
    {
        var piles = new List<PileSave>();
        if (collection?.resources == null) return piles;
        foreach (var p in collection.resources) piles.Add(new PileSave { resource = p.resource.name, amount = p.amount });
        return piles;
    }

    public static ResourceCollection Collection(List<PileSave> piles)
    {
        var collection = new ResourceCollection { resources = new List<ResourcePile>() };
        if (piles == null) return collection;
        foreach (var p in piles)
        {
            var resource = FindResource(p.resource);
            if (resource != null) collection.Add(resource, p.amount);
        }
        return collection;
    }

    public static List<int> Route(Path path, int longitude)
    {
        var route = new List<int>();
        if (path == null) return route;
        foreach (var node in path.nodes) route.Add(node.x * longitude + node.z);
        return route;
    }

    public static Path Route(List<int> route, HexTile[,] tiles, int longitude)
    {
        if (route == null || route.Count == 0) return null;
        var nodes = new List<HexTile>();
        foreach (int i in route) nodes.Add(tiles[i / longitude, i % longitude]);
        return new Path { nodes = nodes };
    }
}
