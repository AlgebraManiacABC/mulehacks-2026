using Unity.VisualScripting;
using UnityEngine;

/**
 * A group of settlers embarking from a settlement
 */
public class SettlerGroup : MonoBehaviour
{
    // The path these settlers are following
    public Path path;
    // Where the Settlers are now, as an index into the Path
    public int path_index;
    // The current resources of the group
    public ResourceCollection resources;
    // The size of the settler group
    public int settlers;

    public int AdvanceTurn()
    {
        // Decrement resources like a Settlement
        // TODO: Implement (return -1 if lost)
        // Move toward goal
        path_index++;
        if (path_index == path.nodes.Count - 1)
        {
            // Reached destination!
            HexTile tile = path.nodes[path_index];
            Settlement sment = tile.AddComponent<Settlement>();
            sment.resources = resources;
            sment.population = settlers;
            sment.tile = tile;
            return 1;
        }
        return 0;
    }
}
