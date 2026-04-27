using UnityEngine;

/// <summary>
/// Moves one placed SimplePoly vehicle prefab along a straight lane.
/// Keep this on the vehicle prefab instance in the scene.
/// </summary>
public class SimplePolyTrafficVehicleMover : MonoBehaviour
{
    public Vector3 laneStart;
    public Vector3 laneEnd;
    public float speed = 5f;
    public float initialProgress;
    public float modelYawOffset;

    private float progress;

    private void Start()
    {
        progress = Mathf.Repeat(initialProgress, 1f);
        MoveToLanePosition();
    }

    private void Update()
    {
        float laneLength = Mathf.Max(Vector3.Distance(laneStart, laneEnd), 0.01f);
        progress += speed * Time.deltaTime / laneLength;

        if (progress > 1f)
        {
            progress -= 1f;
        }

        MoveToLanePosition();
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
        initialProgress = Mathf.Repeat(initialProgress, 1f);
    }

    private void MoveToLanePosition()
    {
        Vector3 direction = laneEnd - laneStart;
        transform.position = Vector3.Lerp(laneStart, laneEnd, progress);

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
        }
    }
}
