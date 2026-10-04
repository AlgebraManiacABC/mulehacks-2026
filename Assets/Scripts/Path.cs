using System.Collections.Generic;
using UnityEngine;

public enum PathMode
{
    // Any terrain, cheapest travel time (roads are never slower than no road)
    TRAVEL,
    // Existing roads only, cheapest travel time
    ROADS_ONLY,
    // Any terrain, preferring to reuse existing roads when planning new construction
    PLAN_ROAD
}

public class Path
{
    // The tiles composing this path, in order
    public List<HexTile> nodes;
    // Edges found dynamically through HexTile::connections

    public int Length => nodes.Count - 1;

    // Turns to cross one step: 1 off-road, 1 / tier on a road
    public static float StepCost(RoadTier tier) => tier == RoadTier.NO_ROAD ? 1f : 1f / (int)tier;

    public float TravelCost()
    {
        float cost = 0f;
        for (int i = 0; i < Length; i++) cost += StepCost(nodes[i].RoadTo(nodes[i + 1]));
        return cost;
    }

    public List<float> StepCosts()
    {
        var costs = new List<float>();
        for (int i = 0; i < Length; i++) costs.Add(StepCost(nodes[i].RoadTo(nodes[i + 1])));
        return costs;
    }

    public int TravelTurns(int multiplier = 1) => Mathf.Max(1, Mathf.CeilToInt(TravelCost() * multiplier - 0.001f));

    // The worst road tier along this path
    public RoadTier MinRoadTier()
    {
        RoadTier min = RoadTier.MAX_TIER;
        for (int i = 0; i < Length; i++)
        {
            RoadTier t = nodes[i].RoadTo(nodes[i + 1]);
            if (t < min) min = t;
        }
        return min;
    }

    // Steps whose road is below the given tier
    public int StepsBelow(RoadTier tier)
    {
        int count = 0;
        for (int i = 0; i < Length; i++)
            if (nodes[i].RoadTo(nodes[i + 1]) < tier) count++;
        return count;
    }
}
