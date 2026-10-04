using UnityEngine;

/**
 * WorldGenerator
 * ----------
 *  Orchestrates generation of Worlds.
 *  Primarily, accepts WorldGenSettings and generates a playable world.
 */
public class WorldGenerator
{
    [Min(1)] public int xWidth = 100;
    [Min(1)] public int zWidth = 100;
    [Min(1)] public int yHeight = 20;
    [Min(0.01f)] public float scale = 20f;
    public int seed = 42;

    public Terrain terrain;

    private int offsetX;
    private int offsetZ;
    
    void InitSeed()
    {
        var rng = new System.Random(seed);
        offsetX = rng.Next(-10000, 10000);
        offsetZ = rng.Next(-10000, 10000);
    }
    
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitSeed();
        terrain.terrainData = GenerateTerrain(terrain.terrainData);
    }

    private void OnValidate()
    {
        InitSeed();
        terrain.terrainData = GenerateTerrain(terrain.terrainData);
    }

    TerrainData GenerateTerrain(TerrainData terrainData)
    {
        int heightmapResolution = Mathf.NextPowerOfTwo(Mathf.Max(xWidth, zWidth))+1;
        terrainData.heightmapResolution = heightmapResolution;
        terrainData.size = new Vector3(xWidth, yHeight, zWidth);
        // These are the heights before we define hexes and biomes
        float[,] heights = GenerateInitialHeights(heightmapResolution);
        // Next step is to generate hexagons
        terrainData.SetHeights(0, 0, heights);
        return terrainData;
    }

    float[,] GenerateInitialHeights(int res)
    {
        float[,] heights = new float[res, res];
        for (int x = 0; x < res; x++)
        {
            for (int z = 0; z < res; z++)
            {
                // Because SetHeights expects z first
                heights[z, x] = CalculateHeight(x, z, res);
            }
        }

        return heights;
    }

    private float CalculateHeight(int x, int z, int res)
    {
        float noiseScale = scale;
        // Normalize x/z
        float xCoord = x * xWidth / (noiseScale * (res - 1));
        float zCoord = z * zWidth / (noiseScale * (res - 1));
        
        return Mathf.PerlinNoise(offsetX + xCoord, offsetZ + zCoord);
    }
}
