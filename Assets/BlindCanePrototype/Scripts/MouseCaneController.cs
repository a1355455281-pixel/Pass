using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Moves the blind cane with the mouse so the player can sweep it across nearby surfaces.
/// This controls only the cane, not the camera.
/// </summary>
public class MouseCaneController : MonoBehaviour
{
    public Transform caneRoot;
    public float mouseSensitivity = 2.4f;
    public float minYaw = -85f;
    public float maxYaw = 85f;
    public float minDownAngle = 20f;
    public float maxDownAngle = 88f;
    public bool lockCursorOnPlay = true;
    public float newInputSystemMouseScale = 0.05f;

    private float yaw;
    private float downAngle = 55f;

    private void Start()
    {
        if (lockCursorOnPlay)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (caneRoot != null)
        {
            Vector3 euler = caneRoot.localEulerAngles;
            downAngle = NormaliseAngle(euler.x);
            yaw = NormaliseAngle(euler.y);
        }
    }

    private void Update()
    {
        if (caneRoot == null)
        {
            return;
        }

        Vector2 mouseDelta = ReadMouseDelta();
        yaw += mouseDelta.x * mouseSensitivity;
        downAngle -= mouseDelta.y * mouseSensitivity;

        yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
        downAngle = Mathf.Clamp(downAngle, minDownAngle, maxDownAngle);

        caneRoot.localRotation = Quaternion.Euler(downAngle, yaw, 0f);

        if (WasEscapePressedThisFrame())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private float NormaliseAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private Vector2 ReadMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue() * newInputSystemMouseScale;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#else
        return Vector2.zero;
#endif
    }

    private bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.escapeKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }
}
