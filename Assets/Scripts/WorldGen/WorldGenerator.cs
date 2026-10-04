using System.Collections.Generic;
using UnityEngine;

/**
 * WorldGenerator
 * ----------
 *  Builds elevation and moisture fields from layered Perlin noise and
 *  gives each tile the biome whose preferred climate is closest.
 */
public class WorldGenerator
{
    public readonly int seed;
    public float[,] elevation;
    public float[,] moisture;
    public BiomeData[,] biomes;

    private readonly WorldGenSettings settings;
    private readonly int width, depth;
    private readonly Vector2 elevationOffset, moistureOffset;

    public WorldGenerator(WorldGenSettings settings, int width, int depth)
    {
        this.settings = settings;
        this.width = width;
        this.depth = depth;
        seed = settings.seed != 0 ? settings.seed : System.Environment.TickCount;
        var rng = new System.Random(seed);
        elevationOffset = new Vector2(rng.Next(-10000, 10000), rng.Next(-10000, 10000));
        moistureOffset = new Vector2(rng.Next(-10000, 10000), rng.Next(-10000, 10000));
    }

    public void Generate(IList<BiomeData> biomeOptions)
    {
        elevation = Normalize(Field(elevationOffset, true));
        moisture = Normalize(Field(moistureOffset, false));
        biomes = new BiomeData[width, depth];
        for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
                biomes[x, z] = ClosestBiome(biomeOptions, elevation[x, z], moisture[x, z]);
    }

    // World-space height of a tile
    public float Height(int x, int z)
    {
        float e = elevation[x, z];
        if (settings.terraces > 0) e = Mathf.Round(e * settings.terraces) / settings.terraces;
        return e * settings.heightScale;
    }

    private float[,] Field(Vector2 offset, bool falloff)
    {
        var field = new float[width, depth];
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                // Odd columns are shifted half a row, as in the hex layout
                float px = x * 0.866f, pz = z + (x % 2 == 1 ? 0.5f : 0f);
                float value = 0f, amplitude = 1f, frequency = 1f / settings.noiseScale;
                for (int o = 0; o < settings.octaves; o++)
                {
                    value += amplitude * Mathf.PerlinNoise(offset.x + px * frequency, offset.y + pz * frequency);
                    amplitude *= 0.5f;
                    frequency *= 2f;
                }
                if (falloff)
                {
                    float ex = Mathf.Min(x, width - 1 - x) / (width / 2f);
                    float ez = Mathf.Min(z, depth - 1 - z) / (depth / 2f);
                    value *= 1f - settings.edgeFalloff * (1f - Mathf.Min(ex, ez));
                }
                field[x, z] = value;
            }
        }
        return field;
    }

    // Stretch to 0..1 so every map uses the full climate range
    private static float[,] Normalize(float[,] field)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (float v in field)
        {
            min = Mathf.Min(min, v);
            max = Mathf.Max(max, v);
        }
        float range = Mathf.Max(max - min, 0.0001f);
        for (int x = 0; x < field.GetLength(0); x++)
            for (int z = 0; z < field.GetLength(1); z++)
                field[x, z] = (field[x, z] - min) / range;
        return field;
    }

    private static BiomeData ClosestBiome(IList<BiomeData> options, float e, float m)
    {
        BiomeData best = null;
        float bestDist = float.MaxValue;
        foreach (var biome in options)
        {
            float de = biome.elevation - e, dm = biome.moisture - m;
            float dist = de * de + dm * dm;
            if (dist >= bestDist) continue;
            bestDist = dist;
            best = biome;
        }
        return best;
    }
}
