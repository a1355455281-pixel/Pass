using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Very small beginner-friendly controller for testing.
/// WASD moves the player and the camera that is parented under it.
/// </summary>
public class BlindWASDPlayerController : MonoBehaviour
{
    public float moveSpeed = 2.5f;
    public float sprintMultiplier = 1.8f;
    public Transform cameraTransform;

    private void Update()
    {
        Vector2 moveInput = ReadMoveInput();

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
        }

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * moveInput.y + right * moveInput.x;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        float speed = IsSprinting() ? moveSpeed * sprintMultiplier : moveSpeed;
        transform.position += move * speed * Time.deltaTime;
    }

    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            Vector2 input = Vector2.zero;

            if (Keyboard.current.aKey.isPressed)
            {
                input.x -= 1f;
            }
            if (Keyboard.current.dKey.isPressed)
            {
                input.x += 1f;
            }
            if (Keyboard.current.sKey.isPressed)
            {
                input.y -= 1f;
            }
            if (Keyboard.current.wKey.isPressed)
            {
                input.y += 1f;
            }

            return input;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }

    private bool IsSprinting()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
#endif
    }
}
