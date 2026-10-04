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

    // Model positions, pointing at corners so they stay clear of roads
    public static readonly Vector3 CenterSlot = Vector3.zero;
    public static readonly Vector3 ResourceSlot = new Vector3(3.4f, 0f, 0f);
    public static readonly Vector3 FarmSlot = new Vector3(-1.7f, 0f, 2.95f);
    public static readonly Vector3 MillSlot = new Vector3(-1.7f, 0f, -2.95f);

    public GameObject Place(GameObject prefab, Vector3 slot, bool animate = false)
    {
        if (prefab == null) return null;
        var obj = Instantiate(prefab, transform);
        obj.transform.localPosition = new Vector3(slot.x, surfaceY - transform.position.y, slot.z);
        if (animate) obj.AddComponent<BuildAnimation>();
        return obj;
    }

    public Vector3 Surface => new Vector3(transform.position.x, surfaceY, transform.position.z);

    public RoadTier RoadTo(HexTile neighbor)
    {
        for (int i = 0; i < connections.Length; i++)
            if (connections[i] == neighbor) return roads[i];
        return RoadTier.NO_ROAD;
    }

    public override string ToString() => "(" + x + ", " + z + ")";
}
