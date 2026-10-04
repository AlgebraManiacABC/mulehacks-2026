using System.Collections.Generic;
using UnityEngine;

public class Path
{
    // The tiles composing this path, in order
    public List<HexTile> nodes;
    // Edges found dynamically through HexTile::connections

    public int Length => nodes.Count - 1;

    // Turns for a shipment along existing roads: each step takes 2 / road tier, rounded up overall
    public int ShippingTurns()
    {
        float turns = 0f;
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            int tier = (int)nodes[i].RoadTo(nodes[i + 1]);
            turns += tier > 0 ? 2f / tier : 2f;
        }
        return Mathf.Max(1, Mathf.CeilToInt(turns));
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
