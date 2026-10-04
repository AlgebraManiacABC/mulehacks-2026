using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/**
 * HexWorldManager
 * ---------------
 *  Is in charge of any loaded world. Manages world creation,
 *   loading, saving, editing. Is in scope whenever a world is present.
 */
public class HexWorldManager : MonoBehaviour
{
    public static HexWorldManager Instance;
    [SerializeField]
    public GameObject hexPrefab;
    // Space to keep track of the current world
    public HexTile[,] tiles;
    public int latitude; // x width in hexes
    public int longitude; // z width in hexes

    [SerializeField]
    public List<BiomeData> biomes;
    public WorldGenSettings worldGen = new WorldGenSettings();
    public Material highlightMaterial;
    public Material roadMaterial;

    // Neighbor offsets (dx, dz) for even and odd columns; odd columns are shifted +z
    private static readonly Vector2Int[] EvenColNeighbors =
        { new(0, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0) };
    private static readonly Vector2Int[] OddColNeighbors =
        { new(0, 1), new(1, 1), new(1, 0), new(0, -1), new(-1, 0), new(-1, 1) };

    private readonly Dictionary<(int, int), Transform> roadSegments = new();
    private GameObject highlight;
    private const float HexRadius = 5f / 0.8660254f; // corner radius for an edge radius of 5

    public int Seed { get; private set; }

    private void Awake()
    {
        if (Instance != null) return;
        Instance = this;

        var save = SaveSystem.PendingLoad;
        if (save != null)
        {
            latitude = save.latitude;
            longitude = save.longitude;
            tiles = new HexTile[latitude, longitude];
            Seed = save.seed;
            foreach (var t in save.tiles)
                CreateTile(t.x, t.z, biomes.Find(b => b.name == t.biome), SaveSystem.FindResource(t.resource), t.height);
        }
        else
        {
            tiles = new HexTile[latitude, longitude];
            var generator = new WorldGenerator(worldGen, latitude, longitude);
            generator.Generate(biomes);
            Random.InitState(generator.seed);
            Seed = generator.seed;
            for (int x = 0; x < latitude; x++)
                for (int z = 0; z < longitude; z++)
                    CreateTile(x, z, generator.biomes[x, z], generator.biomes[x, z].rollResource(), generator.Height(x, z));
        }
        Debug.Log("World seed " + Seed);

        foreach (var tile in tiles)
        {
            var offsets = tile.x % 2 == 0 ? EvenColNeighbors : OddColNeighbors;
            tile.connections = new HexTile[6];
            tile.roads = new RoadTier[6];
            for (int i = 0; i < 6; i++)
            {
                int nx = tile.x + offsets[i].x, nz = tile.z + offsets[i].y;
                if (nx >= 0 && nx < latitude && nz >= 0 && nz < longitude)
                    tile.connections[i] = tiles[nx, nz];
            }
        }
    }

    private void CreateTile(int x, int z, BiomeData biome, ResourceData resource, float height)
    {
        Vector3 position = HexToWorld(x, z, 5f) + Vector3.up * height;
        GameObject curHexObj = Instantiate(hexPrefab, position, Quaternion.identity);
        curHexObj.name = "Hex(" + x + ", " + z + ")";
        HexTile curHex = curHexObj.GetComponent<HexTile>();
        curHex.x = x;
        curHex.z = z;
        curHex.biome = biome;
        MeshRenderer hexRenderer = curHex.GetComponentInChildren<MeshRenderer>();
        if (biome.material != null)
        {
            hexRenderer.material = biome.material;
        }
        curHex.surfaceY = hexRenderer.bounds.max.y;
        // Builds can't cook a MeshCollider from a mesh without Read/Write enabled
        var hexMesh = hexRenderer.GetComponent<MeshFilter>().sharedMesh;
        if (hexMesh.isReadable)
            hexRenderer.gameObject.AddComponent<MeshCollider>().sharedMesh = hexMesh;
        else
        {
            // A box kept inside the hex's edges so it never overlaps a neighbor
            var box = curHexObj.AddComponent<BoxCollider>();
            box.center = hexRenderer.bounds.center - curHexObj.transform.position;
            box.size = new Vector3(8.5f, hexRenderer.bounds.size.y, 8.5f);
        }
        curHex.resource = resource;
        if (resource != null)
        {
            curHex.Place(resource.display, HexTile.ResourceSlot);
        }
        tiles[x, z] = curHex;
    }

    private static Vector3 HexToWorld(int col, int row, float a)
    {
        float x = col * Mathf.Sqrt(3f) * a;
        float z = row * 2f * a + (col % 2 == 1 ? a : 0f);   // shift odd columns
        return new Vector3(x, 0f, z);
    }

    // Number of hex steps between two tiles, ignoring roads
    public static int Distance(HexTile a, HexTile b) => Distance(a.x, a.z, b.x, b.z);

    public static int Distance(int ax, int az, int bx, int bz)
    {
        int aq = ax, ar = az - (ax - (ax & 1)) / 2;
        int bq = bx, br = bz - (bx - (bx & 1)) / 2;
        int dq = bq - aq, dr = br - ar;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
    }

    // Cheapest path between two tiles (Dijkstra); null if unreachable
    public Path FindPath(HexTile start, HexTile goal, PathMode mode = PathMode.ROADS_ONLY)
    {
        var cost = new Dictionary<HexTile, float> { [start] = 0f };
        var cameFrom = new Dictionary<HexTile, HexTile>();
        var done = new HashSet<HexTile>();
        var frontier = new List<HexTile> { start };

        while (frontier.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < frontier.Count; i++)
                if (cost[frontier[i]] < cost[frontier[best]]) best = i;
            var current = frontier[best];
            frontier.RemoveAt(best);
            if (!done.Add(current)) continue;

            if (current == goal)
            {
                var nodes = new List<HexTile>();
                for (var t = goal; t != start; t = cameFrom[t]) nodes.Add(t);
                nodes.Add(start);
                nodes.Reverse();
                return new Path { nodes = nodes };
            }

            for (int i = 0; i < current.connections.Length; i++)
            {
                var next = current.connections[i];
                if (next == null || done.Contains(next)) continue;
                var road = current.roads[i];
                if (mode == PathMode.ROADS_ONLY && road == RoadTier.NO_ROAD) continue;
                float step = mode == PathMode.PLAN_ROAD
                    ? (road == RoadTier.NO_ROAD ? 1f : 0.5f)
                    // A hair extra off-road so equal-cost routes stick to roads
                    : Path.StepCost(road) + (road == RoadTier.NO_ROAD ? 0.001f : 0f);
                float total = cost[current] + step;
                if (cost.TryGetValue(next, out float known) && known <= total) continue;
                cost[next] = total;
                cameFrom[next] = current;
                frontier.Add(next);
            }
        }
        return null;
    }

    // delay staggers the build animation along a multi-tile road
    public void SetRoad(HexTile a, HexTile b, RoadTier tier, float delay = 0f)
    {
        for (int i = 0; i < 6; i++)
        {
            if (a.connections[i] == b) a.roads[i] = tier;
            if (b.connections[i] == a) b.roads[i] = tier;
        }

        int ida = a.GetInstanceID(), idb = b.GetInstanceID();
        var key = ida < idb ? (ida, idb) : (idb, ida);
        bool isNew = !roadSegments.TryGetValue(key, out var road);
        if (isNew)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Road " + a + "-" + b;
            Destroy(obj.GetComponent<Collider>());
            if (roadMaterial != null) obj.GetComponent<Renderer>().sharedMaterial = roadMaterial;
            road = obj.transform;
            road.SetParent(transform);
            road.rotation = Quaternion.LookRotation(b.Surface - a.Surface);
            road.localScale = new Vector3(0f, 0.12f, 0f);
            road.gameObject.AddComponent<RoadAnimation>();
            roadSegments[key] = road;
        }
        float t = (float)tier / (float)RoadTier.MAX_TIER;
        Vector3 lift = Vector3.up * 0.05f;
        road.GetComponent<RoadAnimation>().Play(a.Surface + lift, b.Surface + lift,
            isNew ? 0.5f + 0.7f * t : road.localScale.x, 0.5f + 0.7f * t, isNew, delay);
        var block = new MaterialPropertyBlock();
        // Wooden tiers are brown, stone tiers gray
        block.SetColor("_BaseColor", tier >= RoadTier.TIER_3
            ? (tier == RoadTier.TIER_4 ? new Color(0.72f, 0.72f, 0.75f) : new Color(0.55f, 0.55f, 0.58f))
            : (tier == RoadTier.TIER_2 ? new Color(0.6f, 0.43f, 0.25f) : new Color(0.45f, 0.32f, 0.2f)));
        road.GetComponent<Renderer>().SetPropertyBlock(block);
    }

    // Gold frame around a tile; null hides it
    public void Highlight(HexTile tile)
    {
        if (highlight == null) highlight = BuildFrame(HexRadius * 0.9f, HexRadius * 1.02f);
        highlight.SetActive(tile != null);
        if (tile != null) highlight.transform.position = new Vector3(tile.transform.position.x, tile.surfaceY + 0.08f, tile.transform.position.z);
    }

    private GameObject BuildFrame(float inner, float outer)
    {
        var verts = new Vector3[12];
        var normals = new Vector3[12];
        for (int k = 0; k < 6; k++)
        {
            float angle = k * Mathf.PI / 3f;
            var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            verts[k] = dir * outer;
            verts[k + 6] = dir * inner;
            normals[k] = normals[k + 6] = Vector3.up;
        }
        var tris = new List<int>();
        for (int k = 0; k < 6; k++)
        {
            int o0 = k, o1 = (k + 1) % 6, i0 = k + 6, i1 = (k + 1) % 6 + 6;
            // Both windings so the frame shows regardless of facing
            tris.AddRange(new[] { o0, i0, o1, i0, i1, o1, o0, o1, i0, i0, o1, i1 });
        }
        var mesh = new Mesh { vertices = verts, normals = normals, triangles = tris.ToArray() };

        var frame = new GameObject("Selection Frame");
        frame.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = frame.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = highlightMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return frame;
    }
}
