using UnityEngine;

/**
 * A group of settlers embarking from a settlement
 */
public class SettlerGroup : MonoBehaviour
{
    public HexTile origin;
    public HexTile destination;
    public Path route;
    public int turnsLeft;
    public int totalTurns;
    // The current resources of the group
    public ResourceCollection resources;
    // The size of the settler group
    public int settlers;
    public RouteMover mover;

    /**
     * @return -1 if the group starved, 1 if it reached its destination, 0 otherwise
     */
    public int AdvanceTurn()
    {
        // On the road, each food feeds two settlers
        int needed = (settlers + 1) / 2, eaten = 0;
        foreach (var pile in resources.resources)
        {
            if (eaten == needed) break;
            if (!pile.resource.isFood) continue;
            int bite = Mathf.Min(pile.amount, needed - eaten);
            pile.amount -= bite;
            eaten += bite;
        }
        resources.resources.RemoveAll(p => p.amount <= 0);
        settlers = Mathf.Min(settlers, eaten * 2);
        if (settlers <= 0) return -1;

        turnsLeft--;
        mover.Step();
        return turnsLeft <= 0 ? 1 : 0;
    }
}
