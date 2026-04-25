using UnityEngine;

/// <summary>
/// Reveals a very small area around the player's feet.
/// This gives a tiny sense of immediate ground contact without exposing the full scene.
/// </summary>
public class FootContactRevealer : MonoBehaviour
{
    [Header("Foot Position")]
    public Transform footPoint;
    public Vector3 localFootOffset = new Vector3(0f, 0.05f, 0f);

    [Header("Perception Area")]
    public float footContactRadius = 0.18f;
    public float revealRadius = 0.22f;
    [Range(0f, 1f)] public float footRevealStrength = 0.28f;
    [Range(0f, 1f)] public float footRingStrength = 0f;
    public LayerMask perceivableLayers = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Filtering")]
    public bool ignoreOwnHierarchy = true;

    [Header("Debug")]
    public bool drawDebugFootArea = true;

    private void FixedUpdate()
    {
        Vector3 footPosition = GetFootPosition();

        Collider[] colliders = Physics.OverlapSphere(
            footPosition,
            footContactRadius,
            perceivableLayers,
            triggerInteraction);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider touchedCollider = colliders[i];
            if (touchedCollider == null || ShouldIgnore(touchedCollider.transform))
            {
                continue;
            }

            PerceivableRevealObject perceivable = touchedCollider.GetComponentInParent<PerceivableRevealObject>();
            if (perceivable == null)
            {
                perceivable = touchedCollider.GetComponentInChildren<PerceivableRevealObject>();
            }

            if (perceivable != null)
            {
                Vector3 contactPoint = touchedCollider.ClosestPoint(footPosition);
                // Foot contact should be subtle, so it does not look like a second cane hit.
                perceivable.RevealAt(contactPoint, revealRadius, footRevealStrength, footRingStrength);
            }
        }
    }

    private void OnValidate()
    {
        footContactRadius = Mathf.Max(0.01f, footContactRadius);
        revealRadius = Mathf.Max(0.01f, revealRadius);
        footRevealStrength = Mathf.Clamp01(footRevealStrength);
        footRingStrength = Mathf.Clamp01(footRingStrength);
    }

    private Vector3 GetFootPosition()
    {
        if (footPoint != null)
        {
            return footPoint.position;
        }

        return transform.TransformPoint(localFootOffset);
    }

    private bool ShouldIgnore(Transform touchedTransform)
    {
        if (!ignoreOwnHierarchy || touchedTransform == null)
        {
            return false;
        }

        return touchedTransform == transform || touchedTransform.IsChildOf(transform.root);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugFootArea)
        {
            return;
        }

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 1f);
        Gizmos.DrawWireSphere(GetFootPosition(), footContactRadius);
    }
}
