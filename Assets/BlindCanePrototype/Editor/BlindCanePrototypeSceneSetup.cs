using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BlindCanePrototypeSceneSetup
{
    private const string MainScenePath = "Assets/Scenes/Main.unity";
    private const string RevealMaterialPath = "Assets/BlindCanePrototype/Materials/M_ContactRevealLines.mat";
    private const string HiddenLayerName = "HiddenNormalWorld";
    private const string RevealLayerName = "PerceptionReveal";
    private const string PlayerLayerName = "Player";

    private static readonly Color HiddenGroundColour = new Color(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color HiddenWallColour = new Color(0.55f, 0.18f, 0.12f, 1f);
    private static readonly Color HiddenObstacleColour = new Color(0.12f, 0.28f, 0.55f, 1f);

    [MenuItem("Tools/Blind Cane Prototype/Rebuild Main Test Scene")]
    public static void RebuildMainTestScene()
    {
        EnsureLayers();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Main";

        Material revealMaterial = AssetDatabase.LoadAssetAtPath<Material>(RevealMaterialPath);
        if (revealMaterial == null)
        {
            Debug.LogError("Missing reveal material at " + RevealMaterialPath);
            return;
        }

        int hiddenLayer = LayerMask.NameToLayer(HiddenLayerName);
        int revealLayer = LayerMask.NameToLayer(RevealLayerName);
        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);

        GameObject player = CreatePlayer(playerLayer, revealLayer);
        CreateTestArea(hiddenLayer, revealLayer, revealMaterial);
        CreateLight();

        Selection.activeGameObject = player;
        EditorSceneManager.SaveScene(scene, MainScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Rebuilt blind cane prototype test scene at " + MainScenePath);
    }

    private static GameObject CreatePlayer(int playerLayer, int revealLayer)
    {
        GameObject player = new GameObject("Player_Blind_Cane_Test");
        player.layer = playerLayer;
        player.transform.position = new Vector3(0f, 0f, -1.8f);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.layer = playerLayer;
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.fieldOfView = 65f;
        camera.nearClipPlane = 0.05f;
        camera.cullingMask = (1 << playerLayer) | (1 << revealLayer) | (1 << LayerMask.NameToLayer("UI"));

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
        caneBuilder.contactRadius = 0.09f;
        caneBuilder.revealRadius = 0.45f;
        caneBuilder.perceivableLayers = 1 << revealLayer;
        caneBuilder.caneLayer = playerLayer;
        caneBuilder.BuildOrRebuildCane();

        Transform caneRoot = player.transform.Find(caneBuilder.caneRootName);
        MouseCaneController mouseCane = player.AddComponent<MouseCaneController>();
        mouseCane.caneRoot = caneRoot;
        mouseCane.mouseSensitivity = 2.0f;
        mouseCane.minYaw = -60f;
        mouseCane.maxYaw = 60f;
        mouseCane.minDownAngle = 35f;
        mouseCane.maxDownAngle = 75f;

        GameObject footPoint = new GameObject("FeetContactPoint");
        footPoint.layer = playerLayer;
        footPoint.transform.SetParent(player.transform, false);
        footPoint.transform.localPosition = new Vector3(0f, 0.05f, 0.25f);

        FootContactRevealer footRevealer = player.AddComponent<FootContactRevealer>();
        footRevealer.footPoint = footPoint.transform;
        footRevealer.footContactRadius = 0.22f;
        footRevealer.revealRadius = 0.3f;
        footRevealer.perceivableLayers = 1 << revealLayer;

        return player;
    }

    private static void CreateTestArea(int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        GameObject areaRoot = new GameObject("Prototype_Test_Area");

        CreatePerceivableBlock(areaRoot.transform, "Ground", new Vector3(0f, -0.05f, 0.9f), new Vector3(7f, 0.1f, 7f), HiddenGroundColour, hiddenLayer, revealLayer, revealMaterial, 0.35f);
        CreatePerceivableBlock(areaRoot.transform, "Front_Wall", new Vector3(0f, 1f, 2.15f), new Vector3(4f, 2f, 0.2f), HiddenWallColour, hiddenLayer, revealLayer, revealMaterial, 0.45f);
        CreatePerceivableBlock(areaRoot.transform, "Left_Block", new Vector3(-1.25f, 0.45f, 0.55f), new Vector3(0.75f, 0.9f, 0.75f), HiddenObstacleColour, hiddenLayer, revealLayer, revealMaterial, 0.45f);
        CreatePerceivableBlock(areaRoot.transform, "Right_Pillar", new Vector3(1.35f, 0.75f, 0.85f), new Vector3(0.4f, 1.5f, 0.4f), HiddenObstacleColour, hiddenLayer, revealLayer, revealMaterial, 0.45f);
        CreatePerceivableBlock(areaRoot.transform, "Low_Curb", new Vector3(0f, 0.15f, 0.35f), new Vector3(2.5f, 0.3f, 0.18f), HiddenWallColour, hiddenLayer, revealLayer, revealMaterial, 0.4f);
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
        float revealRadius)
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
        perceivable.maxRevealPoints = 12;
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

    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
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
