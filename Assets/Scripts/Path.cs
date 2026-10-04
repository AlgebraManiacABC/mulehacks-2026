using System.Collections.Generic;

public class Path
{
    // The tiles composing this path, in order
    public List<HexTile> nodes;
    // Edges found dynamically through HexTile::connections

    int calculateCost()
    {
        int cost = 0;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            cost += calculateCost(nodes[i], nodes[i - 1]);
        }

        return cost;
    }

    /**
     * HexTile a and b must be neighbors
     * @return Cost of the trip from a to b, or -1 if not neighbors
     */
    int calculateCost(HexTile a, HexTile b)
    {
        for (int i = 0; i < a.connections.Length; i++)
        {
            if (a.connections[i].Equals(b))
            {
                return (int)((float)RoadTier.MAX_TIER / (int)a.roads[i]);
            }
        }

        return -1;
    }
}
