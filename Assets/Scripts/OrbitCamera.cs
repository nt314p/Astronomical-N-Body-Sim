using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [SerializeField] private Camera cam;
    private Vector3 target;
    private float yaw;
    private float pitch = 30;
    private float radius = 1000f;
    private const float MaxPitchAngle = 89f;

    [SerializeField] private InputAction moveAction;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        target = Vector3.zero;
        moveAction.Enable();
        ProcessOrbit();
    }

    private void Update()
    {
        var movement = moveAction.ReadValue<Vector2>();
        cam.orthographicSize += Mouse.current.scroll.y.ReadValue() * 0.3f;
        cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, 10, 1000);
        
        var camTransform = cam.transform;
        target += camTransform.up * movement.y + camTransform.right * movement.x;

       // yaw += Time.deltaTime * 5f;

        if (Mouse.current.leftButton.isPressed)
            ProcessMouseDelta(Mouse.current.delta.ReadValue());
        
        ProcessOrbit();
    }

    private void ProcessMouseDelta(Vector2 delta)
    {
        pitch -= delta.y;
        yaw += delta.x;

        if (pitch > MaxPitchAngle) pitch = MaxPitchAngle;
        if (pitch < -MaxPitchAngle) pitch = -MaxPitchAngle;
    }

    private void ProcessOrbit()
    {
        var pos = Vector3.back * radius;
        pos = Quaternion.AngleAxis(pitch, Vector3.right) * pos;
        pos = Quaternion.AngleAxis(yaw, Vector3.up) * pos;
        cam.transform.position = pos + target;
        cam.transform.LookAt(target);
    }
}