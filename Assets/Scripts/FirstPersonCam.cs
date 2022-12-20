using UnityEngine;

public class FirstPersonCam : MonoBehaviour
{
    [SerializeField] private float xSensitivity = 4;
    [SerializeField] private float ySensitivity = 4;
    [SerializeField] private float moveSpeed = 80;

    private float pitch;
    private float yaw;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public void ProcessCamera()
    {
        ProcessLook();
        ProcessMovement();
    }

    private void ProcessLook()
    {
        var xAngle = xSensitivity * Input.GetAxis("Mouse X");
        var yAngle = -ySensitivity * Input.GetAxis("Mouse Y");

        var forward = transform.forward;
        forward.y = 0;
        forward = forward.normalized;
        var b = transform.forward;

        var angle = Vector3.Angle(forward, b);

        if (angle > 86)
        {
            if (yAngle < 0 && b.y > 0) // limit top
            {
                yAngle = 0;
            }

            if (yAngle > 0 && b.y < 0) // limit bottom
            {
                yAngle = 0;
            }
        }

        yaw += xAngle;
        pitch += yAngle;

        transform.eulerAngles = new Vector3(pitch, yaw, 0.0f);
    }

    private void ProcessMovement()
    {
        if (Input.GetKey(KeyCode.W))
        {
            var forward = transform.forward;
            forward.y = 0;
            forward.Normalize();
            transform.Translate(forward * (moveSpeed * Time.deltaTime), Space.World);
        }

        if (Input.GetKey(KeyCode.S))
        {
            var forward = transform.forward;
            forward.y = 0;
            forward.Normalize();
            transform.Translate(-forward * (moveSpeed * Time.deltaTime), Space.World);
        }

        if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(-Vector3.right * (moveSpeed * Time.deltaTime));
        }

        if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(Vector3.right * (moveSpeed * Time.deltaTime));
        }

        if (Input.GetKey(KeyCode.Space))
        {
            transform.Translate(Vector3.up * (moveSpeed * Time.deltaTime), Space.World);
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            transform.Translate(-Vector3.up * (moveSpeed * Time.deltaTime), Space.World);
        }
    }
}
