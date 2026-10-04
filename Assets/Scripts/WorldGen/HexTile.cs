using UnityEngine;

/**
 * A single hexagonal tile
 */
public class HexTile : MonoBehaviour
{
    public BiomeData biome;
    // Which sides of this hexagon tile are connected via roads
    public RoadTier[] roads;
    // Which HexTiles this HexTile is connected to (same order as roads)
    public HexTile[] connections;
    // The settlement on the tile, if any
    public Settlement settlement;
    // The resource on this tile
    public ResourceData resource;
    // Grid coordinates
    public int x, z;
    // World-space height of the tile's top face
    public float surfaceY;
    public Renderer hexRenderer;

    public Vector3 Surface => new Vector3(transform.position.x, surfaceY, transform.position.z);

    public RoadTier RoadTo(HexTile neighbor)
    {
        for (int i = 0; i < connections.Length; i++)
            if (connections[i] == neighbor) return roads[i];
        return RoadTier.NO_ROAD;
    }

    public override string ToString() => "(" + x + ", " + z + ")";
}
