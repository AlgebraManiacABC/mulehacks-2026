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
                curHex.biome = RandomBiome();
                if (curHex.biome.material != null)
                {
                    curHex.GetComponentInChildren<MeshRenderer>().material = curHex.biome.material;
                }
                curHex.resource = curHex.biome.rollResource();
                if (curHex.resource != null && curHex.resource.display != null)
                {
                    Instantiate(curHex.resource.display, curHexObj.transform);
                }
                tiles[x, z] = curHex;
            }
        }
    }
    
    private static Vector3 HexToWorld(int col, int row, float a)
    {
        float x = col * Mathf.Sqrt(3f) * a;
        float z = row * 2f * a + (col % 2 == 1 ? a : 0f);   // shift odd columns
        return new Vector3(x, 0f, z);
    }

    // Shortest path (each step costs 1) between two tiles; null if unreachable.
    public Path FindPath(HexTile start, HexTile goal)
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
                if (next == null || current.roads[i] == RoadTier.NO_ROAD) continue;
                if (cameFrom.ContainsKey(next)) continue;
                cameFrom[next] = current;
                frontier.Enqueue(next);
            }
        }
        return null;
    }

    private BiomeData RandomBiome()
    {
        return biomes[Random.Range(0, biomes.Count)];
    }
}
