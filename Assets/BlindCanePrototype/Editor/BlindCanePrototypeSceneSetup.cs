using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BlindCanePrototypeSceneSetup
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string RevealMaterialPath = "Assets/BlindCanePrototype/Materials/M_ContactRevealLines.mat";
    private const string CaneMaterialPath = "Assets/BlindCanePrototype/Materials/M_BlindCaneOutline.mat";
    private const string BakeRequestPath = "Library/BlindCanePrototypeBakeMainScene.request";
    private const string HiddenLayerName = "HiddenNormalWorld";
    private const string RevealLayerName = "PerceptionReveal";
    private const string PlayerLayerName = "Player";
    private const string PlayerName = "Player_Blind_Cane_Test";
    private const string TestAreaName = "Prototype_Test_Area";
    private const string RuntimeBuilderName = "Blind_Cane_TestScene_RuntimeBuilder";

    private static readonly Color HiddenGroundColour = new Color(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color HiddenWallColour = new Color(0.55f, 0.18f, 0.12f, 1f);
    private static readonly Color HiddenObstacleColour = new Color(0.12f, 0.28f, 0.55f, 1f);
    private static readonly Color HiddenTactileColour = new Color(0.95f, 0.76f, 0.08f, 1f);

    [InitializeOnLoadMethod]
    private static void RunRequestedBakeWhenUnityCompiles()
    {
        if (!File.Exists(BakeRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(BakeRequestPath))
            {
                return;
            }

            File.Delete(BakeRequestPath);
            BakePrototypeIntoMainScene();
        };
    }

    [MenuItem("Tools/Blind Cane Prototype/Bake Prototype Into Main Scene")]
    public static void BakePrototypeIntoMainScene()
    {
        EnsureLayers();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        Scene scene = activeScene.path == MainScenePath
            ? activeScene
            : EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        Material revealMaterial = LoadAndConfigureRevealMaterial();
        Material caneMaterial = LoadOrCreateCaneMaterial();

        int hiddenLayer = LayerMask.NameToLayer(HiddenLayerName);
        int revealLayer = LayerMask.NameToLayer(RevealLayerName);
        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);

        DestroyIfFound(PlayerName);
        DestroyIfFound(TestAreaName);
        DestroyIfFound(RuntimeBuilderName);
        DestroyIfFound("Main Camera");

        GameObject player = CreatePlayer(playerLayer, revealLayer, caneMaterial);
        CreateTestArea(hiddenLayer, revealLayer, revealMaterial);
        ConfigureExistingLighting();
        DisableExistingSceneCamerasExcept(player);

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MainScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Baked blind cane prototype directly into " + MainScenePath);
    }

    [MenuItem("Tools/Blind Cane Prototype/Rebuild Main Test Scene")]
    public static void RebuildMainTestScene()
    {
        BakePrototypeIntoMainScene();
    }

    private static GameObject CreatePlayer(int playerLayer, int revealLayer, Material caneMaterial)
    {
        GameObject player = new GameObject(PlayerName);
        player.layer = playerLayer;
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
        camera.cullingMask = (1 << playerLayer) | (1 << revealLayer) | (1 << LayerMask.NameToLayer("UI"));
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<BlindCameraCullingMask>();

        BlindWASDPlayerController movement = player.AddComponent<BlindWASDPlayerController>();
        movement.cameraTransform = cameraObject.transform;
        movement.moveSpeed = 2.2f;

        CanePrimitiveBuilder caneBuilder = player.AddComponent<CanePrimitiveBuilder>();
        caneBuilder.buildOnStart = false;
        caneBuilder.rebuildExistingCane = true;
        caneBuilder.caneMaterial = caneMaterial;
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
        mouseCane.cameraTransform = cameraObject.transform;
        mouseCane.caneMouseSensitivity = 2.4f;
        mouseCane.viewMouseSensitivity = 2f;
        mouseCane.minViewPitch = -55f;
        mouseCane.maxViewPitch = 75f;
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

    private static void CreateTestArea(int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        GameObject areaRoot = new GameObject(TestAreaName);

        CreatePerceivableBlock(areaRoot.transform, "Ground", new Vector3(0f, -0.05f, 0.9f), new Vector3(7f, 0.1f, 7f), HiddenGroundColour, hiddenLayer, revealLayer, revealMaterial, 0.35f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.10f, 0.2f, 0.025f);
        CreateTactilePavingDemo(areaRoot.transform, hiddenLayer, revealLayer, revealMaterial);
        CreatePerceivableBlock(areaRoot.transform, "Front_Wall", new Vector3(0f, 1f, 2.15f), new Vector3(4f, 2f, 0.2f), HiddenWallColour, hiddenLayer, revealLayer, revealMaterial, 0.45f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.12f, 0.22f, 0.025f);
        CreatePerceivableBlock(areaRoot.transform, "Left_Block", new Vector3(-1.25f, 0.45f, 0.55f), new Vector3(0.75f, 0.9f, 0.75f), HiddenObstacleColour, hiddenLayer, revealLayer, revealMaterial, 0.45f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.10f, 0.2f, 0.025f);
        CreatePerceivableBlock(areaRoot.transform, "Right_Pillar", new Vector3(1.35f, 0.75f, 0.85f), new Vector3(0.4f, 1.5f, 0.4f), HiddenObstacleColour, hiddenLayer, revealLayer, revealMaterial, 0.45f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.10f, 0.2f, 0.025f);
        CreatePerceivableBlock(areaRoot.transform, "Low_Curb", new Vector3(0f, 0.15f, 0.35f), new Vector3(2.5f, 0.3f, 0.18f), HiddenWallColour, hiddenLayer, revealLayer, revealMaterial, 0.4f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.12f, 0.2f, 0.025f);
    }

    private static void CreateTactilePavingDemo(Transform parent, int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        Vector3 basePosition = new Vector3(0f, 0.025f, -0.75f);
        CreatePerceivableBlock(parent, "Tactile_Paving_Directional_Base", basePosition, new Vector3(0.95f, 0.05f, 1.35f), HiddenTactileColour, hiddenLayer, revealLayer, revealMaterial, 0.38f, PerceivableRevealObject.SurfaceShapePattern.DirectionalStrips, 0.9f, 0.16f, 0.035f);

        for (int i = 0; i < 5; i++)
        {
            float x = -0.32f + i * 0.16f;
            Vector3 stripPosition = new Vector3(x, 0.075f, -0.75f);
            CreatePerceivableBlock(parent, "Tactile_Paving_Raised_Strip_" + i, stripPosition, new Vector3(0.045f, 0.05f, 1.15f), HiddenTactileColour, hiddenLayer, revealLayer, revealMaterial, 0.32f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.18f, 0.18f, 0.02f);
        }
    }

    private static void CreatePerceivableBlock(
        Transform parent,
        string name,
        Vector3 position,
        Vector3 scale,
        Color hiddenColour,
        int hiddenLayer,
        int revealLayer,
        Material revealMaterial,
        float revealRadius,
        PerceivableRevealObject.SurfaceShapePattern surfacePattern,
        float surfaceStrength,
        float surfaceSpacing,
        float surfaceWidth)
    {
        GameObject hidden = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hidden.name = name + "_HiddenNormalColour";
        hidden.layer = hiddenLayer;
        hidden.transform.SetParent(parent, false);
        hidden.transform.position = position;
        hidden.transform.localScale = scale;
        hidden.GetComponent<Renderer>().sharedMaterial = CreateHiddenMaterial(name, hiddenColour);
        Object.DestroyImmediate(hidden.GetComponent<Collider>());

        GameObject reveal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        reveal.name = name + "_PerceptionReveal";
        reveal.layer = revealLayer;
        reveal.transform.SetParent(parent, false);
        reveal.transform.position = position;
        reveal.transform.localScale = scale;
        reveal.GetComponent<Renderer>().sharedMaterial = revealMaterial;

        PerceivableRevealObject perceivable = reveal.AddComponent<PerceivableRevealObject>();
        perceivable.defaultRevealRadius = revealRadius;
        perceivable.fadeOutSeconds = 0.3f;
        perceivable.maxRevealPoints = 32;
        perceivable.surfacePattern = surfacePattern;
        perceivable.surfaceShapeStrength = surfaceStrength;
        perceivable.surfaceShapeSpacing = surfaceSpacing;
        perceivable.surfaceShapeWidth = surfaceWidth;
    }

    private static Material LoadAndConfigureRevealMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RevealMaterialPath);
        if (material == null)
        {
            Debug.LogError("Missing reveal material at " + RevealMaterialPath);
            return null;
        }

        ConfigureRevealLikeMaterial(material, Color.white, false);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material LoadOrCreateCaneMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CaneMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("BlindPerception/Contact Reveal Lines URP");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, CaneMaterialPath);
        }

        ConfigureRevealLikeMaterial(material, new Color(0.68f, 0.76f, 0.86f, 0.9f), true);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureRevealLikeMaterial(Material material, Color lineColour, bool alwaysVisible)
    {
        if (material == null)
        {
            return;
        }

        SetMaterialColour(material, "_LineColour", lineColour);
        SetMaterialFloat(material, "_RevealRadius", 0.45f);
        SetMaterialFloat(material, "_LineSpacing", 0.16f);
        SetMaterialFloat(material, "_LineWidth", 0.035f);
        SetMaterialFloat(material, "_EdgeSoftness", 0.015f);
        SetMaterialFloat(material, "_SurfaceFill", 0f);
        SetMaterialFloat(material, "_SurfacePattern", alwaysVisible ? 0f : (float)PerceivableRevealObject.SurfaceShapePattern.PlainSurface);
        SetMaterialFloat(material, "_SurfaceShapeStrength", alwaysVisible ? 0f : 0.16f);
        SetMaterialFloat(material, "_SurfaceShapeSpacing", 0.2f);
        SetMaterialFloat(material, "_SurfaceShapeWidth", 0.025f);
        SetMaterialFloat(material, "_ContactRingStrength", 0f);
        SetMaterialFloat(material, "_AlwaysVisible", alwaysVisible ? 1f : 0f);
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetMaterialColour(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, value);
        }
    }

    private static Material CreateHiddenMaterial(string name, Color colour)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.name = "HiddenDebug_" + name;
        material.color = colour;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", colour);
        }

        return material;
    }

    private static void ConfigureExistingLighting()
    {
        Light light = Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return;
        }

        GameObject lightObject = new GameObject("Directional Light");
        Light newLight = lightObject.AddComponent<Light>();
        newLight.type = LightType.Directional;
        newLight.intensity = 1.5f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void DisableExistingSceneCamerasExcept(GameObject player)
    {
        Camera playerCamera = player.GetComponentInChildren<Camera>();
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != playerCamera)
            {
                cameras[i].enabled = false;
            }
        }
    }

    private static void DestroyIfFound(string objectName)
    {
        GameObject existing = GameObject.Find(objectName);
        while (existing != null)
        {
            Object.DestroyImmediate(existing);
            existing = GameObject.Find(objectName);
        }
    }

    private static void EnsureLayers()
    {
        SetLayerName(8, HiddenLayerName);
        SetLayerName(9, RevealLayerName);
        SetLayerName(10, PlayerLayerName);
    }

    private static void SetLayerName(int layerIndex, string layerName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);

        if (string.IsNullOrEmpty(layer.stringValue) || layer.stringValue == layerName)
        {
            layer.stringValue = layerName;
            tagManager.ApplyModifiedProperties();
            return;
        }

        Debug.LogWarning("Layer " + layerIndex + " is already named '" + layer.stringValue + "'. The prototype expected '" + layerName + "'.");
    }
}
