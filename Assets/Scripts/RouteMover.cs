using System.Collections.Generic;
using UnityEngine;

/**
 * Walks a model along a route of tile surfaces, one step per turn
 */
public class RouteMover : MonoBehaviour
{
    public List<Vector3> points;
    public int totalTurns;
    public int turnsElapsed;

    private float shown;
    private bool arrived;
    private Transform[] walkers;
    private Vector3[] walkerBase;

    // Never sits fully on either endpoint until arrival, so it doesn't hide inside a settlement
    private float Target => arrived ? 1f : (turnsElapsed + 1f) / (totalTurns + 2f);

    public static RouteMover Create(GameObject prefab, Path route, int turns)
    {
        var obj = prefab != null ? Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        foreach (var c in obj.GetComponentsInChildren<Collider>()) Destroy(c);
        var mover = obj.AddComponent<RouteMover>();
        mover.points = new List<Vector3>();
        foreach (var node in route.nodes) mover.points.Add(node.Surface);
        mover.totalTurns = Mathf.Max(1, turns);
        obj.transform.position = mover.points[0];
        return mover;
    }

    private void Start()
    {
        // Only the people (capsules) bob while walking
        var people = new List<Transform>();
        foreach (Transform child in transform)
            if (child.name.StartsWith("Capsule")) people.Add(child);
        walkers = people.ToArray();
        walkerBase = new Vector3[walkers.Length];
        for (int i = 0; i < walkers.Length; i++) walkerBase[i] = walkers[i].localPosition;
    }

    public void Step() => turnsElapsed++;

    public void Arrive()
    {
        arrived = true;
        Destroy(gameObject, 1.2f);
    }

    private void Update()
    {
        float target = Target;
        // One turn's step takes about a second
        float speed = (arrived ? 2f : 1f) / (totalTurns + 2f);
        shown = Mathf.MoveTowards(shown, target, speed * Time.deltaTime);
        bool moving = shown < target;

        Vector3 pos = Evaluate(shown, out Vector3 dir);
        transform.position = pos;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);

        for (int i = 0; i < walkers.Length; i++)
        {
            float bob = moving ? Mathf.Abs(Mathf.Sin(Time.time * 10f + i * 1.7f)) * 0.3f : 0f;
            walkers[i].localPosition = walkerBase[i] + Vector3.up * bob;
        }
    }

    private Vector3 Evaluate(float t, out Vector3 dir)
    {
        dir = Vector3.zero;
        if (points.Count < 2) return points[0];
        float segments = points.Count - 1;
        float f = Mathf.Clamp01(t) * segments;
        int i = Mathf.Min((int)f, points.Count - 2);
        dir = points[i + 1] - points[i];
        return Vector3.Lerp(points[i], points[i + 1], f - i);
    }
}
