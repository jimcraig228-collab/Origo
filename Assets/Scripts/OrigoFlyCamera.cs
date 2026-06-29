// OrigoFlyCamera.cs — v1.2, 25 Jun 2026
// Minimal free-fly camera for inspecting the Origo room in Editor and in the standalone build.
// Attach to Main Camera.
//   Move:  W A S D   (forward/left/back/right, relative to look direction)
//   Up/Down: E / Q
//   Look:  hold RIGHT MOUSE BUTTON and move the mouse
//   Faster: hold Left Shift
//   Quit:  Esc  (quits the build; stops play mode in Editor)

using UnityEngine;

public class OrigoFlyCamera : MonoBehaviour
{
    public float moveSpeed = 2.5f;     // metres/sec
    public float fastMultiplier = 3f;
    public float lookSensitivity = 2.5f;

    [Header("Start pose (just inside the door, eye height)")]
    public Vector3 startPosition = new(0.4f, 1.6f, 0.4f);
    public Vector3 startEuler = new(10f, 200f, 0f); // look into room toward E/F corner

    float yaw, pitch;

    void Start()
    {
        transform.position = startPosition;
        transform.eulerAngles = startEuler;
        yaw = startEuler.y; pitch = startEuler.x;
    }

    void Update()
    {
        // Look only while right mouse held (so you can still use the cursor otherwise)
        if (Input.GetMouseButton(1))
        {
            yaw   += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += transform.forward;
        if (Input.GetKey(KeyCode.S)) dir -= transform.forward;
        if (Input.GetKey(KeyCode.D)) dir += transform.right;
        if (Input.GetKey(KeyCode.A)) dir -= transform.right;
        if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) dir -= Vector3.up;
        transform.position += dir.normalized * speed * Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
