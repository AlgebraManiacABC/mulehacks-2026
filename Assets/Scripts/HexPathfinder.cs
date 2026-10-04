using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Axial hex coordinate (flat-topped layout).</summary>
[Serializable]
public struct HexCoord : IEquatable<HexCoord>
{
    public int q;
    public int r;

    public HexCoord(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    public static readonly HexCoord[] Directions =
    {
        new(+1, 0), new(+1, -1), new(0, -1),
        new(-1, 0), new(-1, +1), new(0, +1),
    };

    public IEnumerable<HexCoord> Neighbors()
    {
        foreach (var d in Directions)
            yield return new HexCoord(q + d.q, r + d.r);
    }

    /// <summary>Number of steps between two hexes, ignoring obstacles.</summary>
    public static int Distance(HexCoord a, HexCoord b)
    {
        int dq = a.q - b.q;
        int dr = a.r - b.r;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
    }

    /// <summary>World position on the XZ plane, given the hex inner radius.</summary>
    public Vector3 ToWorld(float innerRadius)
    {
        float x = q * Mathf.Sqrt(3f) * innerRadius;
        float z = (2f * r + q) * innerRadius;
        return new Vector3(x, 0f, z);
    }

    public bool Equals(HexCoord other) => q == other.q && r == other.r;
    public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(q, r);
    public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
    public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
    public override string ToString() => $"({q}, {r})";
}

public static class HexPathfinder
{
    /// <summary>
    /// Shortest path from start to goal where every step costs 1 (breadth-first search).
    /// isWalkable decides which hexes exist and can be entered.
    /// Returns the path including start and goal, or null if the goal is unreachable.
    /// </summary>
    public static List<HexCoord> FindPath(HexCoord start, HexCoord goal, Func<HexCoord, bool> isWalkable)
    {
        if (start == goal) return new List<HexCoord> { start };
        if (!isWalkable(goal)) return null;

        var cameFrom = new Dictionary<HexCoord, HexCoord> { [start] = start };
        var frontier = new Queue<HexCoord>();
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            foreach (var next in current.Neighbors())
            {
                if (cameFrom.ContainsKey(next) || !isWalkable(next)) continue;

                cameFrom[next] = current;
                if (next == goal) return BuildPath(cameFrom, start, goal);
                frontier.Enqueue(next);
            }
        }

        return null;
    }

    static List<HexCoord> BuildPath(Dictionary<HexCoord, HexCoord> cameFrom, HexCoord start, HexCoord goal)
    {
        var path = new List<HexCoord>();
        for (var c = goal; c != start; c = cameFrom[c])
            path.Add(c);
        path.Add(start);
        path.Reverse();
        return path;
    }
}
