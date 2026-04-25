using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds a ready-to-test blind cane prototype when Main scene enters Play Mode.
/// This keeps the scene beginner-friendly: open Main, press Play, test with WASD and mouse.
/// </summary>
public class BlindCanePrototypeRuntimeSceneBuilder : MonoBehaviour
{
    public bool buildOnlyInMainScene = true;
    public bool rebuildExistingPrototype = true;
    public string playerName = "Player_Blind_Cane_Test";
    public string testAreaName = "Prototype_Test_Area";

    private const string HiddenLayerName = "HiddenNormalWorld";
    private const string RevealLayerName = "PerceptionReveal";
    private const string PlayerLayerName = "Player";

    private void Start()
    {
        if (buildOnlyInMainScene && SceneManager.GetActiveScene().name != "Main")
        {
            return;
        }

        BuildScene();
    }

    [ContextMenu("Build Prototype Now")]
    public void BuildScene()
    {
        if (rebuildExistingPrototype)
        {
            DestroyIfFound(playerName);
            DestroyIfFound(testAreaName);
        }
        else if (GameObject.Find(playerName) != null)
        {
            return;
        }

        int hiddenLayer = GetLayerOrDefault(HiddenLayerName);
        int revealLayer = GetLayerOrDefault(RevealLayerName);
        int playerLayer = GetLayerOrDefault(PlayerLayerName);

        Material revealMaterial = CreateRevealMaterial();

        GameObject player = CreatePlayer(playerLayer, revealLayer);
        CreateTestArea(hiddenLayer, revealLayer, revealMaterial);
        DisableExistingSceneCamerasExcept(player);
    }

    private GameObject CreatePlayer(int playerLayer, int revealLayer)
    {
        GameObject player = new GameObject(playerName);
        SetLayerRecursively(player, playerLayer);
        player.transform.position = new Vector3(0f, 0f, -1.8f);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.layer = playerLayer;
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.55f, -0.1f);
        cameraObject.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 78f;
        camera.nearClipPlane = 0.05f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = (1 << playerLayer) | (1 << revealLayer) | (1 << GetLayerOrDefault("UI"));
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<BlindCameraCullingMask>();

        BlindWASDPlayerController movement = player.AddComponent<BlindWASDPlayerController>();
        movement.cameraTransform = cameraObject.transform;
        movement.moveSpeed = 2.2f;

        CanePrimitiveBuilder caneBuilder = player.AddComponent<CanePrimitiveBuilder>();
        caneBuilder.buildOnStart = false;
        caneBuilder.rebuildExistingCane = true;
        caneBuilder.localPosition = new Vector3(0.33f, 1.1f, 0.2f);
        caneBuilder.localEulerAngles = new Vector3(55f, 0f, 0f);
        caneBuilder.shaftLength = 1.45f;
        caneBuilder.shaftThickness = 0.035f;
        caneBuilder.tipRadius = 0.075f;
        caneBuilder.generatedCaneColour = new Color(0.68f, 0.76f, 0.86f, 0.9f);
        caneBuilder.contactRadius = 0.09f;
        caneBuilder.revealRadius = 0.45f;
        caneBuilder.perceivableLayers = 1 << revealLayer;
        caneBuilder.caneLayer = playerLayer;
        caneBuilder.BuildOrRebuildCane();

        Transform caneRoot = player.transform.Find(caneBuilder.caneRootName);
        MouseCaneController mouseCane = player.AddComponent<MouseCaneController>();
        mouseCane.caneRoot = caneRoot;
        mouseCane.mouseSensitivity = 2.4f;
        mouseCane.minYaw = -85f;
        mouseCane.maxYaw = 85f;
        mouseCane.minDownAngle = 20f;
        mouseCane.maxDownAngle = 88f;

        GameObject footPoint = new GameObject("FeetContactPoint");
        footPoint.layer = playerLayer;
        footPoint.transform.SetParent(player.transform, false);
        footPoint.transform.localPosition = new Vector3(0f, 0.05f, 0.25f);

        FootContactRevealer footRevealer = player.AddComponent<FootContactRevealer>();
        footRevealer.footPoint = footPoint.transform;
        footRevealer.footContactRadius = 0.18f;
        footRevealer.revealRadius = 0.22f;
        footRevealer.footRevealStrength = 0.28f;
        footRevealer.footRingStrength = 0f;
        footRevealer.perceivableLayers = 1 << revealLayer;

        return player;
    }

    private void CreateTestArea(int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        GameObject areaRoot = new GameObject(testAreaName);

        CreatePerceivableBlock(areaRoot.transform, "Ground", new Vector3(0f, -0.05f, 0.9f), new Vector3(7f, 0.1f, 7f), new Color(0.22f, 0.22f, 0.22f, 1f), hiddenLayer, revealLayer, revealMaterial, 0.35f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.10f, 0.2f, 0.025f);
        CreateTactilePavingDemo(areaRoot.transform, hiddenLayer, revealLayer, revealMaterial);
        CreatePerceivableBlock(areaRoot.transform, "Front_Wall", new Vector3(0f, 1f, 2.15f), new Vector3(4f, 2f, 0.2f), new Color(0.55f, 0.18f, 0.12f, 1f), hiddenLayer, revealLayer, revealMaterial, 0.45f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.12f, 0.22f, 0.025f);
        CreatePerceivableBlock(areaRoot.transform, "Left_Block", new Vector3(-1.25f, 0.45f, 0.55f), new Vector3(0.75f, 0.9f, 0.75f), new Color(0.12f, 0.28f, 0.55f, 1f), hiddenLayer, revealLayer, revealMaterial, 0.45f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.10f, 0.2f, 0.025f);
        CreatePerceivableBlock(areaRoot.transform, "Right_Pillar", new Vector3(1.35f, 0.75f, 0.85f), new Vector3(0.4f, 1.5f, 0.4f), new Color(0.12f, 0.28f, 0.55f, 1f), hiddenLayer, revealLayer, revealMaterial, 0.45f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.10f, 0.2f, 0.025f);
        CreatePerceivableBlock(areaRoot.transform, "Low_Curb", new Vector3(0f, 0.15f, 0.35f), new Vector3(2.5f, 0.3f, 0.18f), new Color(0.55f, 0.18f, 0.12f, 1f), hiddenLayer, revealLayer, revealMaterial, 0.4f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.12f, 0.2f, 0.025f);
    }

    private void CreateTactilePavingDemo(Transform parent, int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        Color yellow = new Color(0.95f, 0.76f, 0.08f, 1f);
        Vector3 basePosition = new Vector3(0f, 0.025f, -0.75f);

        CreatePerceivableBlock(parent, "Tactile_Paving_Directional_Base", basePosition, new Vector3(0.95f, 0.05f, 1.35f), yellow, hiddenLayer, revealLayer, revealMaterial, 0.38f, PerceivableRevealObject.SurfaceShapePattern.DirectionalStrips, 0.9f, 0.16f, 0.035f);

        for (int i = 0; i < 5; i++)
        {
            float x = -0.32f + i * 0.16f;
            Vector3 stripPosition = new Vector3(x, 0.075f, -0.75f);
            CreatePerceivableBlock(parent, "Tactile_Paving_Raised_Strip_" + i, stripPosition, new Vector3(0.045f, 0.05f, 1.15f), yellow, hiddenLayer, revealLayer, revealMaterial, 0.32f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.18f, 0.18f, 0.02f);
        }
    }

    private void CreatePerceivableBlock(Transform parent, string name, Vector3 position, Vector3 scale, Color hiddenColour, int hiddenLayer, int revealLayer, Material revealMaterial, float revealRadius, PerceivableRevealObject.SurfaceShapePattern surfacePattern, float surfaceStrength, float surfaceSpacing, float surfaceWidth)
    {
        GameObject hidden = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hidden.name = name + "_HiddenNormalColour";
        hidden.layer = hiddenLayer;
        hidden.transform.SetParent(parent, false);
        hidden.transform.position = position;
        hidden.transform.localScale = scale;
        hidden.GetComponent<Renderer>().material = CreateHiddenMaterial(hiddenColour);
        Destroy(hidden.GetComponent<Collider>());

        GameObject reveal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        reveal.name = name + "_PerceptionReveal";
        reveal.layer = revealLayer;
        reveal.transform.SetParent(parent, false);
        reveal.transform.position = position;
        reveal.transform.localScale = scale;
        reveal.GetComponent<Renderer>().material = revealMaterial;

        PerceivableRevealObject perceivable = reveal.AddComponent<PerceivableRevealObject>();
        perceivable.defaultRevealRadius = revealRadius;
        perceivable.fadeOutSeconds = 0.3f;
        perceivable.maxRevealPoints = 12;
        perceivable.surfacePattern = surfacePattern;
        perceivable.surfaceShapeStrength = surfaceStrength;
        perceivable.surfaceShapeSpacing = surfaceSpacing;
        perceivable.surfaceShapeWidth = surfaceWidth;
    }

    private Material CreateRevealMaterial()
    {
        Shader shader = Shader.Find("BlindPerception/Contact Reveal Lines URP");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader);
        material.name = "Runtime_Contact_Reveal_Lines";
        if (material.HasProperty("_LineColour"))
        {
            material.SetColor("_LineColour", Color.white);
        }
        if (material.HasProperty("_RevealRadius"))
        {
            material.SetFloat("_RevealRadius", 0.45f);
        }
        if (material.HasProperty("_LineSpacing"))
        {
            material.SetFloat("_LineSpacing", 0.16f);
        }
        if (material.HasProperty("_LineWidth"))
        {
            material.SetFloat("_LineWidth", 0.035f);
        }
        if (material.HasProperty("_EdgeSoftness"))
        {
            material.SetFloat("_EdgeSoftness", 0.015f);
        }
        if (material.HasProperty("_SurfaceFill"))
        {
            material.SetFloat("_SurfaceFill", 0f);
        }
        if (material.HasProperty("_SurfacePattern"))
        {
            material.SetFloat("_SurfacePattern", (float)PerceivableRevealObject.SurfaceShapePattern.PlainSurface);
        }
        if (material.HasProperty("_SurfaceShapeStrength"))
        {
            material.SetFloat("_SurfaceShapeStrength", 0.16f);
        }
        if (material.HasProperty("_SurfaceShapeSpacing"))
        {
            material.SetFloat("_SurfaceShapeSpacing", 0.2f);
        }
        if (material.HasProperty("_SurfaceShapeWidth"))
        {
            material.SetFloat("_SurfaceShapeWidth", 0.025f);
        }
        if (material.HasProperty("_ContactRingStrength"))
        {
            material.SetFloat("_ContactRingStrength", 0f);
        }
        if (material.HasProperty("_AlwaysVisible"))
        {
            material.SetFloat("_AlwaysVisible", 0f);
        }
        return material;
    }

    private Material CreateHiddenMaterial(Color colour)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = colour;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", colour);
        }

        return material;
    }

    private int GetLayerOrDefault(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? layer : 0;
    }

    private void DisableExistingSceneCamerasExcept(GameObject player)
    {
        Camera playerCamera = player.GetComponentInChildren<Camera>();
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != playerCamera)
            {
                cameras[i].enabled = false;
            }
        }
    }

    private void DestroyIfFound(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        if (existing != null)
        {
            Destroy(existing);
        }
    }

    private void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        for (int i = 0; i < target.transform.childCount; i++)
        {
            SetLayerRecursively(target.transform.GetChild(i).gameObject, layer);
        }
    }
}
