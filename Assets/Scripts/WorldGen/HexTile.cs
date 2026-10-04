using UnityEngine;

/**
 * A single hexagonal tile
 */
public class HexTile : MonoBehaviour
{
    public BiomeData biome;
    // Which sides of this hexagon tile are connected via roads
    public RoadTier[] connections;
    // The settlement on the tile, if any
    public Settlement settlement;
    // The resource on this tile
    public ResourceData resource;
}
