using UnityEngine;

/**
 * Raises a model out of the ground, or sinks it back in and destroys it
 */
public class BuildAnimation : MonoBehaviour
{
    public float duration = 1.2f;

    private Vector3 top, bottom;
    private float elapsed;
    private bool sinking;

    private void Awake()
    {
        top = transform.localPosition;
        float height = 1f;
        foreach (var r in GetComponentsInChildren<Renderer>())
            height = Mathf.Max(height, r.bounds.max.y - transform.position.y);
        bottom = top - Vector3.up * (height + 0.1f);
        transform.localPosition = bottom;
    }

    public void Sink()
    {
        sinking = true;
        elapsed = 0f;
        top = transform.localPosition;
        Destroy(gameObject, duration);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
        transform.localPosition = sinking ? Vector3.Lerp(top, bottom, t) : Vector3.Lerp(bottom, top, t);
        // A little shake while moving
        float shake = t < 1f ? Mathf.Sin(elapsed * 40f) * 0.04f : 0f;
        transform.localPosition += new Vector3(shake, 0f, 0f);
        if (!sinking && t >= 1f) enabled = false;
    }
}
