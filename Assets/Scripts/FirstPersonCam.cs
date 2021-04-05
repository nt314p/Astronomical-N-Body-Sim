using System.Globalization;
using UnityEditor;
using UnityEngine;

public class FirstPersonCam : MonoBehaviour
{
    [SerializeField] private float xSensitivity = 4;
    [SerializeField] private float ySensitivity = 4;
    [SerializeField] private float moveSpeed = 80;
    [SerializeField] private AstronomicalRunner astroRunner;

    private float pitch = 0;
    private float yaw = 0;

    private bool isFrozen = false;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            isFrozen = !isFrozen;
            astroRunner.Freeze(isFrozen);
        }

        if (Input.GetKeyDown((KeyCode.F1)))
        {
            var rt = astroRunner.GetRenderTexture();
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;

            var bytes = texture.EncodeToPNG();
            var name = System.DateTime.Now.ToString(CultureInfo.CurrentCulture);
            name = name.Replace('/', '-');
            var path = "Assets/Resources/screenshot.png";
            System.IO.File.WriteAllBytes(path, bytes);
            Debug.Log("Saved screenshot!");
        }
        ProcessLook();
        ProcessMovement();
    }

    private void ProcessLook()
    {
        var xAngle = xSensitivity * Input.GetAxis("Mouse X");
        var yAngle = -ySensitivity * Input.GetAxis("Mouse Y");

        Vector3 a = transform.forward;
        a.y = 0;
        a = a.normalized;
        Vector3 b = transform.forward;

        float angle = Vector3.Angle(a, b);

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
