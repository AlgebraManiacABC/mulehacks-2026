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

    // Neighbor offsets (dx, dz) for even and odd columns; odd columns are shifted +z
    private static readonly Vector2Int[] EvenColNeighbors =
        { new(0, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0) };
    private static readonly Vector2Int[] OddColNeighbors =
        { new(0, 1), new(1, 1), new(1, 0), new(0, -1), new(-1, 0), new(-1, 1) };

    private readonly Dictionary<(int, int), LineRenderer> roadLines = new();
    private Material roadMaterial;

    private void Awake()
    {
        if (Instance != null) return;
        Instance = this;
        // The scene was just loaded; just create a world for now
        Random.InitState(Time.frameCount);
        
        // Load prefab hex for each position
        tiles = new HexTile[latitude, longitude];
        for (int x = 0; x < latitude; x++)
        {
            for (int z = 0; z < longitude; z++)
            {
                GameObject curHexObj = Instantiate(hexPrefab, HexToWorld(x, z, 5f), Quaternion.identity);
                curHexObj.name = "Hex(" + x + ", " + z + ")";
                HexTile curHex = curHexObj.GetComponent<HexTile>();
                curHex.x = x;
                curHex.z = z;
                curHex.biome = RandomBiome();
                MeshRenderer hexRenderer = curHex.GetComponentInChildren<MeshRenderer>();
                curHex.hexRenderer = hexRenderer;
                if (curHex.biome.material != null)
                {
                    hexRenderer.material = curHex.biome.material;
                }
                curHex.surfaceY = hexRenderer.bounds.max.y;
                var meshCollider = hexRenderer.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = hexRenderer.GetComponent<MeshFilter>().sharedMesh;
                curHex.resource = curHex.biome.rollResource();
                if (curHex.resource != null && curHex.resource.display != null)
                {
                    Instantiate(curHex.resource.display, curHexObj.transform);
                }
                tiles[x, z] = curHex;
            }
        }

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
    
    private static Vector3 HexToWorld(int col, int row, float a)
    {
        float x = col * Mathf.Sqrt(3f) * a;
        float z = row * 2f * a + (col % 2 == 1 ? a : 0f);   // shift odd columns
        return new Vector3(x, 0f, z);
    }

    // Number of hex steps between two tiles, ignoring roads
    public static int Distance(HexTile a, HexTile b)
    {
        int aq = a.x, ar = a.z - (a.x - (a.x & 1)) / 2;
        int bq = b.x, br = b.z - (b.x - (b.x & 1)) / 2;
        int dq = bq - aq, dr = br - ar;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
    }

    // Shortest path (each step costs 1) between two tiles; null if unreachable.
    // With roadsOnly, only existing roads may be travelled.
    public Path FindPath(HexTile start, HexTile goal, bool roadsOnly = true)
    {
        var cameFrom = new Dictionary<HexTile, HexTile> { [start] = start };
        var frontier = new Queue<HexTile>();
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
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
                if (next == null || (roadsOnly && current.roads[i] == RoadTier.NO_ROAD)) continue;
                if (cameFrom.ContainsKey(next)) continue;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }
        return null;
    }

    public void SetRoad(HexTile a, HexTile b, RoadTier tier)
    {
        for (int i = 0; i < 6; i++)
        {
            if (a.connections[i] == b) a.roads[i] = tier;
            if (b.connections[i] == a) b.roads[i] = tier;
        }

        int ida = a.GetInstanceID(), idb = b.GetInstanceID();
        var key = ida < idb ? (ida, idb) : (idb, ida);
        if (!roadLines.TryGetValue(key, out var line))
        {
            if (roadMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                roadMaterial = new Material(shader);
            }
            line = new GameObject("Road " + a + "-" + b).AddComponent<LineRenderer>();
            line.transform.SetParent(transform);
            line.material = roadMaterial;
            line.positionCount = 2;
            line.SetPosition(0, a.Surface + Vector3.up * 0.2f);
            line.SetPosition(1, b.Surface + Vector3.up * 0.2f);
            roadLines[key] = line;
        }
        float t = (float)tier / (float)RoadTier.MAX_TIER;
        line.widthMultiplier = 0.4f + 0.6f * t;
        line.startColor = line.endColor = Color.Lerp(new Color(0.55f, 0.4f, 0.25f), new Color(0.75f, 0.75f, 0.8f), t);
    }

    private BiomeData RandomBiome()
    {
        return biomes[Random.Range(0, biomes.Count)];
    }
}
