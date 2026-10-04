using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    private InputAction move;
    private float yaw, pitch;
    private bool movingCamera = false;

    [SerializeField] private float initialY = 10f;
    [SerializeField] float panSpeed = 40f;          // units per second
    [SerializeField] float lookSensitivity = 0.15f; // degrees per pixel
    [SerializeField] float minPitch = -89f, maxPitch = 89f;
    [SerializeField] float focusDistance = 35f;
    [SerializeField] float minFocusPitch = 35f;
    
    float smoothYaw, smoothPitch;   // current (displayed) angles
    Vector3 targetPosition;
    
    [Header("Smoothing (higher = snappier)")]
    [SerializeField] float lookSmoothing = 15f;
    [SerializeField] float panSmoothing = 10f;
    
    void Awake()
    {
        gameObject.transform.position = new Vector3(0, initialY, 0);
        move = InputSystem.actions.FindAction("Move");
        Vector3 e = transform.eulerAngles;
        yaw = smoothYaw = e.y;
        pitch = smoothPitch = e.x > 180f ? e.x - 360f : e.x;   // convert 0–360 to -180–180
        targetPosition = transform.position;
    }

    // Move so the camera looks at point; snap skips the smoothing
    public void FocusOn(Vector3 point, bool snap)
    {
        pitch = Mathf.Max(pitch, minFocusPitch);
        targetPosition = point - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * focusDistance;
        if (!snap) return;
        smoothYaw = yaw;
        smoothPitch = pitch;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = targetPosition;
    }

    void Update()
    {
        var mouse = Mouse.current;

        // Aim: hold right mouse and drag (updates the target only)
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();  // already per-frame, no deltaTime
            yaw   += delta.x * lookSensitivity;
            pitch -= delta.y * lookSensitivity;
            pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Pan: WASD on the ground plane, relative to the target facing (updates the target only)
        Vector2 input = move.ReadValue<Vector2>();
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        targetPosition += (flat * Vector3.right * input.x + flat * Vector3.forward * input.y)
                          * panSpeed * Time.deltaTime;

        // Smoothing: same feel at any frame rate
        float lookT = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);
        float panT  = 1f - Mathf.Exp(-panSmoothing  * Time.deltaTime);

        smoothYaw   = Mathf.Lerp(smoothYaw,   yaw,   lookT);
        smoothPitch = Mathf.Lerp(smoothPitch, pitch, lookT);
        transform.rotation = Quaternion.Euler(smoothPitch, smoothYaw, 0f);
        transform.position = Vector3.Lerp(transform.position, targetPosition, panT);
    }
}
