using UnityEngine;

/**
 * A group of settlers embarking from a settlement
 */
public class SettlerGroup : MonoBehaviour
{
    public HexTile origin;
    public HexTile destination;
    public int turnsLeft;
    public int totalTurns;
    // The current resources of the group
    public ResourceCollection resources;
    // The size of the settler group
    public int settlers;

    /**
     * @return -1 if the group starved, 1 if it reached its destination, 0 otherwise
     */
    public int AdvanceTurn()
    {
        int hungry = settlers;
        foreach (var pile in resources.resources)
        {
            if (hungry == 0) break;
            if (!pile.resource.isFood) continue;
            int eaten = Mathf.Min(pile.amount, hungry);
            pile.amount -= eaten;
            hungry -= eaten;
        }
        resources.resources.RemoveAll(p => p.amount <= 0);
        settlers -= hungry;
        if (settlers <= 0) return -1;

        turnsLeft--;
        float progress = 1f - (float)turnsLeft / totalTurns;
        transform.position = Vector3.Lerp(origin.Surface, destination.Surface, progress) + Vector3.up;
        return turnsLeft <= 0 ? 1 : 0;
    }
}
