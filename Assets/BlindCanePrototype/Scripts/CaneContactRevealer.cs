using UnityEngine;

/// <summary>
/// Samples the cane with small casts and sends contact points to perceivable objects.
/// This is direct contact through the cane, not sonar or distance vision.
/// </summary>
public class CaneContactRevealer : MonoBehaviour
{
    [Header("Cane Segment")]
    public bool detectWholeCane = true;
    public Transform caneBaseTransform;
    public Transform tipTransform;
    public float contactRadius = 0.08f;
    [Range(2, 12)] public int wholeCaneSampleCount = 7;

    [Header("Reveal")]
    public float revealRadius = 0.45f;
    public LayerMask perceivableLayers = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [Header("Filtering")]
    public bool ignoreOwnHierarchy = true;

    [Header("Debug")]
    public bool drawDebugContactSphere = true;

    private Vector3[] previousSamplePositions;
    private bool hasPreviousSamplePositions;

    private void Reset()
    {
        tipTransform = transform;
    }

    private void OnEnable()
    {
        EnsureSampleBuffer();
        CacheCurrentSamplePositions();
        hasPreviousSamplePositions = true;
    }

    private void FixedUpdate()
    {
        Vector3 currentBasePosition = GetBasePosition();
        Vector3 currentTipPosition = GetTipPosition();
        EnsureSampleBuffer();

        if (!hasPreviousSamplePositions)
        {
            CacheCurrentSamplePositions(currentBasePosition, currentTipPosition);
            hasPreviousSamplePositions = true;
        }

        if (detectWholeCane)
        {
            SweepWholeCane(currentBasePosition, currentTipPosition);
            CheckCurrentCaneOverlap(currentBasePosition, currentTipPosition);
        }
        else
        {
            SweepBetween(previousSamplePositions[0], currentTipPosition);
            CheckCurrentTipOverlap(currentTipPosition);
        }

        CacheCurrentSamplePositions(currentBasePosition, currentTipPosition);
    }

    private void OnValidate()
    {
        contactRadius = Mathf.Max(0.001f, contactRadius);
        revealRadius = Mathf.Max(0.01f, revealRadius);
        wholeCaneSampleCount = Mathf.Clamp(wholeCaneSampleCount, 2, 12);
    }

    private Vector3 GetBasePosition()
    {
        if (caneBaseTransform != null)
        {
            return caneBaseTransform.position;
        }

        Transform parentTransform = tipTransform != null ? tipTransform.parent : transform.parent;
        return parentTransform != null ? parentTransform.position : transform.position;
    }

    private Vector3 GetTipPosition()
    {
        return tipTransform != null ? tipTransform.position : transform.position;
    }

    private void EnsureSampleBuffer()
    {
        int requiredSize = detectWholeCane ? wholeCaneSampleCount : 1;

        if (previousSamplePositions == null || previousSamplePositions.Length != requiredSize)
        {
            previousSamplePositions = new Vector3[requiredSize];
            hasPreviousSamplePositions = false;
        }
    }

    private void CacheCurrentSamplePositions()
    {
        CacheCurrentSamplePositions(GetBasePosition(), GetTipPosition());
    }

    private void CacheCurrentSamplePositions(Vector3 basePosition, Vector3 tipPosition)
    {
        int sampleCount = previousSamplePositions.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount <= 1 ? 1f : i / (sampleCount - 1f);
            previousSamplePositions[i] = Vector3.Lerp(basePosition, tipPosition, t);
        }
    }

    private void SweepWholeCane(Vector3 currentBasePosition, Vector3 currentTipPosition)
    {
        int sampleCount = previousSamplePositions.Length;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = sampleCount <= 1 ? 1f : i / (sampleCount - 1f);
            Vector3 currentSamplePosition = Vector3.Lerp(currentBasePosition, currentTipPosition, t);
            SweepBetween(previousSamplePositions[i], currentSamplePosition);
        }
    }

    private void SweepBetween(Vector3 start, Vector3 end)
    {
        Vector3 movement = end - start;
        float distance = movement.magnitude;

        if (distance <= 0.001f)
        {
            return;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            start,
            contactRadius,
            movement.normalized,
            distance + contactRadius,
            perceivableLayers,
            triggerInteraction);

        for (int i = 0; i < hits.Length; i++)
        {
            Vector3 contactPoint = GetReliableSphereCastPoint(hits[i], end);
            ReportHit(hits[i].collider, contactPoint);
        }
    }

    private void CheckCurrentTipOverlap(Vector3 tipPosition)
    {
        Collider[] colliders = Physics.OverlapSphere(
            tipPosition,
            contactRadius,
            perceivableLayers,
            triggerInteraction);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider touchedCollider = colliders[i];
            Vector3 contactPoint = touchedCollider.ClosestPoint(tipPosition);

            // If the tip is inside the collider, ClosestPoint can return the same position.
            // That is still a useful direct-contact point for this prototype.
            ReportHit(touchedCollider, contactPoint);
        }
    }

    private void CheckCurrentCaneOverlap(Vector3 basePosition, Vector3 tipPosition)
    {
        Collider[] colliders = Physics.OverlapCapsule(
            basePosition,
            tipPosition,
            contactRadius,
            perceivableLayers,
            triggerInteraction);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider touchedCollider = colliders[i];
            if (touchedCollider == null)
            {
                continue;
            }

            Vector3 contactPoint = GetClosestObjectPointToCane(touchedCollider, basePosition, tipPosition);
            ReportHit(touchedCollider, contactPoint);
        }
    }

    private void ReportHit(Collider touchedCollider, Vector3 contactPoint)
    {
        if (touchedCollider == null || ShouldIgnore(touchedCollider.transform))
        {
            return;
        }

        PerceivableRevealObject perceivable = touchedCollider.GetComponentInParent<PerceivableRevealObject>();
        if (perceivable == null)
        {
            perceivable = touchedCollider.GetComponentInChildren<PerceivableRevealObject>();
        }

        if (perceivable != null)
        {
            perceivable.RevealAt(contactPoint, revealRadius);
        }
    }

    private Vector3 GetClosestObjectPointToCane(Collider touchedCollider, Vector3 basePosition, Vector3 tipPosition)
    {
        Vector3 closestObjectPoint = touchedCollider.ClosestPoint(tipPosition);
        float closestDistanceSqr = float.PositiveInfinity;
        int sampleCount = Mathf.Max(wholeCaneSampleCount, 2);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (sampleCount - 1f);
            Vector3 canePoint = Vector3.Lerp(basePosition, tipPosition, t);
            Vector3 objectPoint = touchedCollider.ClosestPoint(canePoint);
            float distanceSqr = (objectPoint - canePoint).sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestObjectPoint = objectPoint;
            }
        }

        return closestObjectPoint;
    }

    private Vector3 GetReliableSphereCastPoint(RaycastHit hit, Vector3 fallbackTipPosition)
    {
        if (hit.collider == null)
        {
            return fallbackTipPosition;
        }

        // Unity can report a zero point when a SphereCast starts inside a collider.
        // Use the current tip position instead so the reveal stays where the cane is touching.
        if (hit.distance <= 0.0001f && hit.point == Vector3.zero)
        {
            return hit.collider.ClosestPoint(fallbackTipPosition);
        }

        return hit.point;
    }

    private bool ShouldIgnore(Transform touchedTransform)
    {
        if (!ignoreOwnHierarchy || touchedTransform == null)
        {
            return false;
        }

        Transform ownRoot = transform.root;
        return touchedTransform == transform || touchedTransform.IsChildOf(ownRoot);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugContactSphere)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Vector3 basePosition = GetBasePosition();
        Vector3 tipPosition = GetTipPosition();
        Gizmos.DrawLine(basePosition, tipPosition);
        Gizmos.DrawWireSphere(basePosition, contactRadius);
        Gizmos.DrawWireSphere(tipPosition, contactRadius);
    }
}
