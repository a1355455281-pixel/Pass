using UnityEngine;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Moves the view with the mouse, or the cane while the left mouse button is held.
/// This lets the player look around normally, then deliberately sweep the cane.
/// </summary>
public class MouseCaneController : MonoBehaviour
{
    [Header("References")]
    public Transform caneRoot;
    public Transform cameraTransform;

    [Header("Cane Drag")]
    [FormerlySerializedAs("mouseSensitivity")]
    public float caneMouseSensitivity = 2.4f;
    public float minYaw = -85f;
    public float maxYaw = 85f;
    public float minDownAngle = 20f;
    public float maxDownAngle = 88f;

    [Header("View Look")]
    public float viewMouseSensitivity = 2.0f;
    public float minViewPitch = -55f;
    public float maxViewPitch = 75f;

    [Header("Cursor")]
    public bool lockCursorOnPlay = true;
    public float newInputSystemMouseScale = 0.05f;

    private float caneYaw;
    private float downAngle = 55f;
    private float viewYaw;
    private float viewPitch;

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
            caneYaw = NormaliseAngle(euler.y);
        }

        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
            }
            else
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                cameraTransform = childCamera != null ? childCamera.transform : null;
            }
        }

        viewYaw = NormaliseAngle(transform.localEulerAngles.y);
        if (cameraTransform != null)
        {
            viewPitch = NormaliseAngle(cameraTransform.localEulerAngles.x);
        }
    }

    private void Update()
    {
        if (WasEscapePressedThisFrame())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (lockCursorOnPlay && Cursor.lockState == CursorLockMode.None && WasAnyMouseButtonPressedThisFrame())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Vector2 mouseDelta = ReadMouseDelta();
        if (mouseDelta == Vector2.zero)
        {
            return;
        }

        if (IsLeftMouseButtonHeld())
        {
            MoveCane(mouseDelta);
        }
        else
        {
            MoveView(mouseDelta);
        }
    }

    private void MoveCane(Vector2 mouseDelta)
    {
        if (caneRoot == null)
        {
            return;
        }

        caneYaw += mouseDelta.x * caneMouseSensitivity;
        downAngle -= mouseDelta.y * caneMouseSensitivity;

        caneYaw = Mathf.Clamp(caneYaw, minYaw, maxYaw);
        downAngle = Mathf.Clamp(downAngle, minDownAngle, maxDownAngle);

        caneRoot.localRotation = Quaternion.Euler(downAngle, caneYaw, 0f);
    }

    private void MoveView(Vector2 mouseDelta)
    {
        if (cameraTransform == null)
        {
            return;
        }

        viewYaw += mouseDelta.x * viewMouseSensitivity;
        viewPitch -= mouseDelta.y * viewMouseSensitivity;
        viewPitch = Mathf.Clamp(viewPitch, minViewPitch, maxViewPitch);

        transform.localRotation = Quaternion.Euler(0f, viewYaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(viewPitch, 0f, 0f);
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

    private bool IsLeftMouseButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#else
        return false;
#endif
    }

    private bool WasAnyMouseButtonPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame
                || Mouse.current.middleButton.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
#else
        return false;
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
