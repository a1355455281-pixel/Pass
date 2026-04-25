using UnityEngine;

/// <summary>
/// Builds a simple blind cane from Unity primitive objects.
/// Attach this to the player, then use the context menu or let it build on Start.
/// </summary>
public class CanePrimitiveBuilder : MonoBehaviour
{
    [Header("Build")]
    public bool buildOnStart = true;
    public bool rebuildExistingCane = false;
    public string caneRootName = "Blind_Cane_Root";

    [Header("Placement")]
    public Vector3 localPosition = new Vector3(0.35f, 0.85f, 0.45f);
    public Vector3 localEulerAngles = new Vector3(55f, 0f, 0f);

    [Header("Shape")]
    public float shaftLength = 1.35f;
    public float shaftThickness = 0.035f;
    public float tipRadius = 0.07f;
    public bool addHandle = true;

    [Header("Contact")]
    public bool addContactRevealer = true;
    public float contactRadius = 0.08f;
    public float revealRadius = 0.45f;
    public LayerMask perceivableLayers = ~0;

    [Header("Visuals")]
    public Material caneMaterial;
    public Color generatedCaneColour = new Color(0.68f, 0.76f, 0.86f, 0.9f);
    public int caneLayer = 0;

    private void Start()
    {
        if (buildOnStart)
        {
            BuildOrRebuildCane();
        }
    }

    [ContextMenu("Build Or Rebuild Cane")]
    public void BuildOrRebuildCane()
    {
        Transform existingCane = transform.Find(caneRootName);
        if (existingCane != null)
        {
            if (!rebuildExistingCane)
            {
                return;
            }

            DestroyObject(existingCane.gameObject);
        }

        Material material = caneMaterial != null ? caneMaterial : CreateRuntimeCaneMaterial();

        GameObject caneRoot = new GameObject(caneRootName);
        caneRoot.layer = caneLayer;
        caneRoot.transform.SetParent(transform, false);
        caneRoot.transform.localPosition = localPosition;
        caneRoot.transform.localEulerAngles = localEulerAngles;

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Cane_Shaft";
        shaft.layer = caneLayer;
        shaft.transform.SetParent(caneRoot.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 0f, shaftLength * 0.5f);
        shaft.transform.localScale = new Vector3(shaftThickness, shaftThickness, shaftLength);
        SetMaterial(shaft, material);
        RemoveCollider(shaft);

        if (addHandle)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Cane_Handle";
            handle.layer = caneLayer;
            handle.transform.SetParent(caneRoot.transform, false);
            handle.transform.localPosition = Vector3.zero;
            handle.transform.localScale = new Vector3(0.28f, shaftThickness * 1.4f, shaftThickness * 1.4f);
            SetMaterial(handle, material);
            RemoveCollider(handle);
        }

        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Cane_Tip_Contact_Sphere";
        tip.layer = caneLayer;
        tip.transform.SetParent(caneRoot.transform, false);
        tip.transform.localPosition = new Vector3(0f, 0f, shaftLength);
        tip.transform.localScale = Vector3.one * (tipRadius * 2f);
        SetMaterial(tip, material);

        SphereCollider tipCollider = tip.GetComponent<SphereCollider>();
        tipCollider.isTrigger = true;

        if (addContactRevealer)
        {
            CaneContactRevealer revealer = tip.GetComponent<CaneContactRevealer>();
            if (revealer == null)
            {
                revealer = tip.AddComponent<CaneContactRevealer>();
            }

            revealer.tipTransform = tip.transform;
            revealer.caneBaseTransform = caneRoot.transform;
            revealer.detectWholeCane = true;
            revealer.contactRadius = contactRadius;
            revealer.wholeCaneSampleCount = 7;
            revealer.revealRadius = revealRadius;
            revealer.perceivableLayers = perceivableLayers;
        }
    }

    private Material CreateRuntimeCaneMaterial()
    {
        Shader shader = Shader.Find("BlindPerception/Contact Reveal Lines URP");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "Runtime_Blind_Cane_Outline_Material";

        if (material.HasProperty("_LineColour"))
        {
            material.SetColor("_LineColour", generatedCaneColour);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", generatedCaneColour);
        }
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", generatedCaneColour);
        }
        if (material.HasProperty("_AlwaysVisible"))
        {
            material.SetFloat("_AlwaysVisible", 1f);
        }
        if (material.HasProperty("_SurfaceFill"))
        {
            material.SetFloat("_SurfaceFill", 0f);
        }
        if (material.HasProperty("_SurfacePattern"))
        {
            material.SetFloat("_SurfacePattern", 0f);
        }
        if (material.HasProperty("_SurfaceShapeStrength"))
        {
            material.SetFloat("_SurfaceShapeStrength", 0f);
        }
        if (material.HasProperty("_ContactRingStrength"))
        {
            material.SetFloat("_ContactRingStrength", 0f);
        }
        if (material.HasProperty("_LineWidth"))
        {
            material.SetFloat("_LineWidth", 0.035f);
        }
        if (material.HasProperty("_EdgeSoftness"))
        {
            material.SetFloat("_EdgeSoftness", 0.015f);
        }

        return material;
    }

    private void SetMaterial(GameObject target, Material material)
    {
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyObject(collider);
        }
    }

    private void DestroyObject(Object target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
