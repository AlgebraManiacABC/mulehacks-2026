using UnityEngine;

[System.Serializable]
public class WorldGenSettings
{
    // 0 picks a random seed each game
    public int seed = 0;
    // Larger values make bigger biome regions
    [Min(0.5f)] public float noiseScale = 6f;
    [Range(1, 6)] public int octaves = 3;
    // How strongly elevation drops toward the map edge (pushes coasts outward)
    [Range(0f, 1f)] public float edgeFalloff = 0.35f;
    // World-space height of the highest tiles
    public float heightScale = 1.5f;
    // Elevation is rounded to this many terrace steps (0 = smooth)
    public int terraces = 4;
}
