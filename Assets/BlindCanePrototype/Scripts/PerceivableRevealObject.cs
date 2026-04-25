using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores recent cane or foot contact points and sends them to the reveal material.
/// The material stays invisible until a contact point is close to a pixel on the mesh.
/// </summary>
public class PerceivableRevealObject : MonoBehaviour
{
    private const int MaxShaderPoints = 16;

    public enum SurfaceShapePattern
    {
        None = 0,
        PlainSurface = 1,
        DirectionalStrips = 2,
        WarningDots = 3,
        CrossHatch = 4
    }

    [Header("Reveal Timing")]
    public float defaultRevealRadius = 0.45f;
    public float fadeOutSeconds = 0.3f;
    public float minimumPointSpacing = 0.08f;
    [Range(1, MaxShaderPoints)] public int maxRevealPoints = 12;

    [Header("Outline Visual")]
    public Color lineColour = Color.white;
    public float lineSpacing = 0.16f;
    public float lineWidth = 0.035f;
    public float lineEdgeSoftness = 0.015f;

    [Header("Surface Shape")]
    public SurfaceShapePattern surfacePattern = SurfaceShapePattern.PlainSurface;
    [Range(0f, 1f)] public float surfaceShapeStrength = 0.16f;
    public float surfaceShapeSpacing = 0.2f;
    public float surfaceShapeWidth = 0.025f;
    [Range(0f, 1f)] public float surfaceFill = 0f;

    [Header("Renderers")]
    public bool includeChildRenderers = true;

    private readonly List<RevealPoint> revealPoints = new List<RevealPoint>();
    private readonly Vector4[] shaderPoints = new Vector4[MaxShaderPoints];
    private readonly float[] shaderRadii = new float[MaxShaderPoints];
    private readonly float[] shaderRingStrengths = new float[MaxShaderPoints];

    private Renderer[] renderersToUpdate;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int RevealCountId = Shader.PropertyToID("_RevealCount");
    private static readonly int RevealPointsId = Shader.PropertyToID("_RevealPoints");
    private static readonly int RevealRadiiId = Shader.PropertyToID("_RevealRadii");
    private static readonly int RevealRingStrengthsId = Shader.PropertyToID("_RevealRingStrengths");
    private static readonly int RevealRadiusId = Shader.PropertyToID("_RevealRadius");
    private static readonly int LineColourId = Shader.PropertyToID("_LineColour");
    private static readonly int LineSpacingId = Shader.PropertyToID("_LineSpacing");
    private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
    private static readonly int EdgeSoftnessId = Shader.PropertyToID("_EdgeSoftness");
    private static readonly int SurfacePatternId = Shader.PropertyToID("_SurfacePattern");
    private static readonly int SurfaceShapeStrengthId = Shader.PropertyToID("_SurfaceShapeStrength");
    private static readonly int SurfaceShapeSpacingId = Shader.PropertyToID("_SurfaceShapeSpacing");
    private static readonly int SurfaceShapeWidthId = Shader.PropertyToID("_SurfaceShapeWidth");
    private static readonly int SurfaceFillId = Shader.PropertyToID("_SurfaceFill");

    private struct RevealPoint
    {
        public Vector3 position;
        public float radius;
        public float strength;
        public float ringStrength;
        public float age;

        public RevealPoint(Vector3 position, float radius, float strength, float ringStrength)
        {
            this.position = position;
            this.radius = radius;
            this.strength = strength;
            this.ringStrength = ringStrength;
            age = 0f;
        }
    }

    private void Awake()
    {
        RefreshRenderers();
        SendRevealDataToMaterials();
    }

    private void OnEnable()
    {
        RefreshRenderers();
        SendRevealDataToMaterials();
    }

    private void OnValidate()
    {
        defaultRevealRadius = Mathf.Max(0.01f, defaultRevealRadius);
        fadeOutSeconds = Mathf.Max(0.01f, fadeOutSeconds);
        minimumPointSpacing = Mathf.Max(0f, minimumPointSpacing);
        maxRevealPoints = Mathf.Clamp(maxRevealPoints, 1, MaxShaderPoints);
        lineSpacing = Mathf.Max(0.01f, lineSpacing);
        lineWidth = Mathf.Max(0.001f, lineWidth);
        lineEdgeSoftness = Mathf.Max(0.0001f, lineEdgeSoftness);
        surfaceShapeStrength = Mathf.Clamp01(surfaceShapeStrength);
        surfaceShapeSpacing = Mathf.Max(0.001f, surfaceShapeSpacing);
        surfaceShapeWidth = Mathf.Max(0.001f, surfaceShapeWidth);
        surfaceFill = Mathf.Clamp01(surfaceFill);
    }

    private void Update()
    {
        if (revealPoints.Count == 0)
        {
            return;
        }

        for (int i = revealPoints.Count - 1; i >= 0; i--)
        {
            RevealPoint point = revealPoints[i];
            point.age += Time.deltaTime;

            if (point.age >= fadeOutSeconds)
            {
                revealPoints.RemoveAt(i);
            }
            else
            {
                revealPoints[i] = point;
            }
        }

        SendRevealDataToMaterials();
    }

    public void RevealAt(Vector3 worldPoint)
    {
        RevealAt(worldPoint, defaultRevealRadius, 1f, 1f);
    }

    public void RevealAt(Vector3 worldPoint, float radius)
    {
        RevealAt(worldPoint, radius, 1f, 1f);
    }

    public void RevealAt(Vector3 worldPoint, float radius, float strength)
    {
        RevealAt(worldPoint, radius, strength, 1f);
    }

    public void RevealAt(Vector3 worldPoint, float radius, float strength, float ringStrength)
    {
        radius = radius > 0f ? radius : defaultRevealRadius;
        strength = Mathf.Clamp01(strength);
        ringStrength = Mathf.Clamp01(ringStrength);

        int nearbyPointIndex = FindNearbyPoint(worldPoint);
        if (nearbyPointIndex >= 0)
        {
            revealPoints[nearbyPointIndex] = new RevealPoint(worldPoint, radius, strength, ringStrength);
        }
        else
        {
            if (revealPoints.Count >= maxRevealPoints)
            {
                RemoveOldestPoint();
            }

            revealPoints.Add(new RevealPoint(worldPoint, radius, strength, ringStrength));
        }

        SendRevealDataToMaterials();
    }

    [ContextMenu("Refresh Renderers")]
    public void RefreshRenderers()
    {
        renderersToUpdate = includeChildRenderers
            ? GetComponentsInChildren<Renderer>()
            : GetComponents<Renderer>();

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private int FindNearbyPoint(Vector3 worldPoint)
    {
        float minimumDistanceSqr = minimumPointSpacing * minimumPointSpacing;

        for (int i = 0; i < revealPoints.Count; i++)
        {
            if ((revealPoints[i].position - worldPoint).sqrMagnitude <= minimumDistanceSqr)
            {
                return i;
            }
        }

        return -1;
    }

    private void RemoveOldestPoint()
    {
        int oldestIndex = 0;
        float oldestAge = revealPoints[0].age;

        for (int i = 1; i < revealPoints.Count; i++)
        {
            if (revealPoints[i].age > oldestAge)
            {
                oldestAge = revealPoints[i].age;
                oldestIndex = i;
            }
        }

        revealPoints.RemoveAt(oldestIndex);
    }

    private void SendRevealDataToMaterials()
    {
        if (renderersToUpdate == null || renderersToUpdate.Length == 0)
        {
            return;
        }

        for (int i = 0; i < MaxShaderPoints; i++)
        {
            shaderPoints[i] = Vector4.zero;
            shaderRadii[i] = defaultRevealRadius;
            shaderRingStrengths[i] = 0f;
        }

        int pointCount = Mathf.Min(revealPoints.Count, MaxShaderPoints);
        for (int i = 0; i < pointCount; i++)
        {
            RevealPoint point = revealPoints[i];
            float fadeAlpha = 1f - Mathf.Clamp01(point.age / fadeOutSeconds);
            shaderPoints[i] = new Vector4(point.position.x, point.position.y, point.position.z, fadeAlpha * point.strength);
            shaderRadii[i] = point.radius;
            shaderRingStrengths[i] = point.ringStrength;
        }

        for (int i = 0; i < renderersToUpdate.Length; i++)
        {
            Renderer targetRenderer = renderersToUpdate[i];
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(RevealCountId, pointCount);
            propertyBlock.SetVectorArray(RevealPointsId, shaderPoints);
            propertyBlock.SetFloatArray(RevealRadiiId, shaderRadii);
            propertyBlock.SetFloatArray(RevealRingStrengthsId, shaderRingStrengths);
            propertyBlock.SetFloat(RevealRadiusId, defaultRevealRadius);
            propertyBlock.SetColor(LineColourId, lineColour);
            propertyBlock.SetFloat(LineSpacingId, lineSpacing);
            propertyBlock.SetFloat(LineWidthId, lineWidth);
            propertyBlock.SetFloat(EdgeSoftnessId, lineEdgeSoftness);
            propertyBlock.SetFloat(SurfacePatternId, (float)surfacePattern);
            propertyBlock.SetFloat(SurfaceShapeStrengthId, surfaceShapeStrength);
            propertyBlock.SetFloat(SurfaceShapeSpacingId, surfaceShapeSpacing);
            propertyBlock.SetFloat(SurfaceShapeWidthId, surfaceShapeWidth);
            propertyBlock.SetFloat(SurfaceFillId, surfaceFill);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
