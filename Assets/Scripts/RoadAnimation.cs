using UnityEngine;

/**
 * Grows a road segment out from its start, or eases an existing one to a new width
 */
public class RoadAnimation : MonoBehaviour
{
    public float delay;
    public float duration = 0.5f;

    private Vector3 from, to;
    private float startWidth, endWidth;
    private bool grow;
    private float elapsed;

    public void Play(Vector3 start, Vector3 end, float oldWidth, float newWidth, bool growFromStart, float wait)
    {
        from = start;
        to = end;
        startWidth = oldWidth;
        endWidth = newWidth;
        grow = growFromStart;
        delay = wait;
        elapsed = 0f;
        enabled = true;
        Apply(0f);
    }

    private void Update()
    {
        if (delay > 0f)
        {
            delay -= Time.deltaTime;
            return;
        }
        elapsed += Time.deltaTime;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
        Apply(t);
        if (t >= 1f) enabled = false;
    }

    private void Apply(float t)
    {
        float length = Vector3.Distance(from, to);
        float shown = grow ? length * t : length;
        transform.position = from + (to - from).normalized * (shown / 2f);
        var scale = transform.localScale;
        transform.localScale = new Vector3(Mathf.Lerp(startWidth, endWidth, t), scale.y, shown);
    }
}
