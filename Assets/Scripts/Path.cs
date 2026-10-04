using System.Collections.Generic;

public class Path
{
    // The tiles composing this path, in order
    public List<HexTile> nodes;
    // Edges found dynamically through HexTile::connections

    public int Length => nodes.Count - 1;

    public int CalculateCost()
    {
        int cost = 0;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            cost += CalculateCost(nodes[i], nodes[i + 1]);
        }

        return cost;
    }

    /**
     * HexTile a and b must be neighbors
     * @return Cost of the trip from a to b, or -1 if not neighbors or not connected by road
     */
    public static int CalculateCost(HexTile a, HexTile b)
    {
        for (int i = 0; i < a.connections.Length; i++)
        {
            if (a.connections[i] == b)
            {
                if (a.roads[i] == RoadTier.NO_ROAD) return -1;
                return (int)RoadTier.MAX_TIER / (int)a.roads[i];
            }
        }

        return -1;
    }

    // The worst road tier along this path
    public RoadTier MinRoadTier()
    {
        RoadTier min = RoadTier.MAX_TIER;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            RoadTier t = nodes[i].RoadTo(nodes[i + 1]);
            if (t < min) min = t;
        }
        return min;
    }
}
