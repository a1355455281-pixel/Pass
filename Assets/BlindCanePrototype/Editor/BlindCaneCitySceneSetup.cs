using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BlindCaneCitySceneSetup
{
    private const string TestScenePath = "Assets/Scenes/Testlevel.unity";
    private const string RevealMaterialPath = "Assets/BlindCanePrototype/Materials/M_ContactRevealLines.mat";
    private const string CaneMaterialPath = "Assets/BlindCanePrototype/Materials/M_BlindCaneOutline.mat";
    private const string BakeRequestPath = "Library/BlindCaneCityBakeTestScene.request";

    private const string HiddenLayerName = "HiddenNormalWorld";
    private const string RevealLayerName = "PerceptionReveal";
    private const string PlayerLayerName = "Player";

    private const string PlayerName = "Player_Blind_Cane_Test";
    private const string CityRootName = "Blind_Cane_SimplePoly_City_Map";

    private const string RoadPrefabFolder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Roads/";
    private const string BuildingPrefabFolder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Buildings/";
    private const string PropPrefabFolder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Props/";
    private const string VehiclePrefabFolder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Static Wheels/";

    private const float RoadStep = 20f;
    private const float SidewalkX = -12f;

    [InitializeOnLoadMethod]
    private static void RunRequestedBakeWhenUnityCompiles()
    {
        QueueRequestedBakeIfNeeded();
    }

    private static void QueueRequestedBakeIfNeeded()
    {
        if (!File.Exists(BakeRequestPath))
        {
            return;
        }

        EditorApplication.delayCall -= TryRunRequestedBake;
        EditorApplication.delayCall += TryRunRequestedBake;
    }

    private static void TryRunRequestedBake()
    {
        if (!File.Exists(BakeRequestPath))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= RunRequestedBakeAfterPlayMode;
            EditorApplication.playModeStateChanged += RunRequestedBakeAfterPlayMode;
            Debug.Log("Blind Cane city scene bake is waiting until Play Mode exits.");
            return;
        }

        File.Delete(BakeRequestPath);
        BakeCityMapIntoTestScene();
    }

    private static void RunRequestedBakeAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        EditorApplication.playModeStateChanged -= RunRequestedBakeAfterPlayMode;
        QueueRequestedBakeIfNeeded();
    }

    [MenuItem("Tools/Blind Cane Prototype/Bake City Map Into Testlevel")]
    public static void BakeCityMapIntoTestScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            File.WriteAllText(BakeRequestPath, "bake-after-play-mode");
            EditorApplication.playModeStateChanged -= RunRequestedBakeAfterPlayMode;
            EditorApplication.playModeStateChanged += RunRequestedBakeAfterPlayMode;
            Debug.Log("Blind Cane city scene bake was queued because Unity is in Play Mode.");
            return;
        }

        EnsureLayers();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }

        Scene scene = activeScene.path == TestScenePath
            ? activeScene
            : EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);

        Material revealMaterial = LoadRevealMaterial();
        Material caneMaterial = LoadCaneMaterial();

        int hiddenLayer = LayerMask.NameToLayer(HiddenLayerName);
        int revealLayer = LayerMask.NameToLayer(RevealLayerName);
        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);

        DestroyIfFound(PlayerName);
        DestroyIfFound(CityRootName);
        DestroyIfFound("Blind_Cane_City_Map");
        DestroyIfFound("Main Camera");

        GameObject player = CreatePlayer(playerLayer, revealLayer, caneMaterial);
        CreateSimplePolyCityMap(hiddenLayer, revealLayer, revealMaterial);
        ConfigureLighting();
        DisableExistingSceneCamerasExcept(player);

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, TestScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Baked SimplePoly City prefab test map directly into " + TestScenePath);
    }

    private static GameObject CreatePlayer(int playerLayer, int revealLayer, Material caneMaterial)
    {
        GameObject player = new GameObject(PlayerName);
        player.layer = playerLayer;
        player.transform.position = new Vector3(SidewalkX, 0f, -39f);

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.layer = playerLayer;
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.55f, -0.08f);
        cameraObject.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

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
        movement.moveSpeed = 3.2f;

        CanePrimitiveBuilder caneBuilder = player.AddComponent<CanePrimitiveBuilder>();
        caneBuilder.buildOnStart = false;
        caneBuilder.rebuildExistingCane = true;
        caneBuilder.caneMaterial = caneMaterial;
        caneBuilder.localPosition = new Vector3(0.33f, 1.08f, 0.22f);
        caneBuilder.localEulerAngles = new Vector3(58f, 0f, 0f);
        caneBuilder.shaftLength = 1.65f;
        caneBuilder.shaftThickness = 0.035f;
        caneBuilder.tipRadius = 0.075f;
        caneBuilder.contactRadius = 0.1f;
        caneBuilder.revealRadius = 0.65f;
        caneBuilder.perceivableLayers = 1 << revealLayer;
        caneBuilder.caneLayer = playerLayer;
        caneBuilder.BuildOrRebuildCane();

        Transform caneRoot = player.transform.Find(caneBuilder.caneRootName);
        MouseCaneController mouseCane = player.AddComponent<MouseCaneController>();
        mouseCane.caneRoot = caneRoot;
        mouseCane.cameraTransform = cameraObject.transform;
        mouseCane.caneMouseSensitivity = 2.7f;
        mouseCane.viewMouseSensitivity = 2f;
        mouseCane.minViewPitch = -55f;
        mouseCane.maxViewPitch = 75f;
        mouseCane.minYaw = -120f;
        mouseCane.maxYaw = 120f;
        mouseCane.minDownAngle = 18f;
        mouseCane.maxDownAngle = 90f;

        GameObject footPoint = new GameObject("FeetContactPoint");
        footPoint.layer = playerLayer;
        footPoint.transform.SetParent(player.transform, false);
        footPoint.transform.localPosition = new Vector3(0f, 0.06f, 0.22f);

        FootContactRevealer footRevealer = player.AddComponent<FootContactRevealer>();
        footRevealer.footPoint = footPoint.transform;
        footRevealer.footContactRadius = 0.24f;
        footRevealer.revealRadius = 0.32f;
        footRevealer.footRevealStrength = 0.34f;
        footRevealer.footRingStrength = 0f;
        footRevealer.perceivableLayers = 1 << revealLayer;

        return player;
    }

    private static void CreateSimplePolyCityMap(int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        GameObject cityRoot = new GameObject(CityRootName);

        Transform roads = CreateChildRoot(cityRoot.transform, "SimplePoly_Road_Prefabs");
        Transform buildings = CreateChildRoot(cityRoot.transform, "SimplePoly_Building_Prefabs");
        Transform props = CreateChildRoot(cityRoot.transform, "SimplePoly_Street_Prop_Prefabs");
        Transform tactileRoute = CreateChildRoot(cityRoot.transform, "SimplePoly_Tactile_Paving_Route");
        Transform traffic = CreateChildRoot(cityRoot.transform, "SimplePoly_Vehicle_Prefabs");

        CreateRoadPrefabs(roads, hiddenLayer, revealLayer, revealMaterial);
        CreateBuildingPrefabs(buildings, hiddenLayer, revealLayer, revealMaterial);
        CreateStreetPropPrefabs(props, hiddenLayer, revealLayer, revealMaterial);
        CreateTactilePavingPrefabs(tactileRoute, hiddenLayer, revealLayer, revealMaterial);
        CreateVehiclePrefabs(traffic, revealLayer, revealMaterial);
    }

    private static void CreateRoadPrefabs(Transform parent, int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        for (int zIndex = -2; zIndex <= 2; zIndex++)
        {
            float z = zIndex * RoadStep;
            string roadName = zIndex == 0 ? "Road Intersection_01.prefab" : "Road Lane_01.prefab";
            CreatePerceivablePrefab(parent, RoadPrefabFolder + roadName, "North_South_Road_" + zIndex, new Vector3(0f, 0f, z), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Road);
        }

        for (int xIndex = -2; xIndex <= 2; xIndex++)
        {
            if (xIndex == 0)
            {
                continue;
            }

            float x = xIndex * RoadStep;
            CreatePerceivablePrefab(parent, RoadPrefabFolder + "Road Lane_01.prefab", "East_West_Road_" + xIndex, new Vector3(x, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Road);
        }

        for (int zIndex = -2; zIndex <= 2; zIndex++)
        {
            float z = zIndex * RoadStep;
            CreatePerceivablePrefab(parent, RoadPrefabFolder + "Road Sidewalk.prefab", "West_Sidewalk_" + zIndex, new Vector3(SidewalkX, 0f, z), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Sidewalk);
            CreatePerceivablePrefab(parent, RoadPrefabFolder + "Road Sidewalk.prefab", "East_Sidewalk_" + zIndex, new Vector3(-SidewalkX, 0f, z), Quaternion.Euler(0f, 180f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Sidewalk);
        }

        CreatePerceivablePrefab(parent, RoadPrefabFolder + "Road Split Line.prefab", "Centre_Road_Line_South", new Vector3(0f, 0.03f, -20f), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.RoadMarking);
        CreatePerceivablePrefab(parent, RoadPrefabFolder + "Road Split Line.prefab", "Centre_Road_Line_North", new Vector3(0f, 0.03f, 20f), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.RoadMarking);
    }

    private static void CreateBuildingPrefabs(Transform parent, int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        CreatePerceivablePrefab(parent, BuildingPrefabFolder + "Building_Coffee Shop.prefab", "Coffee_Shop", new Vector3(-31f, 0f, -28f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Building);
        CreatePerceivablePrefab(parent, BuildingPrefabFolder + "Building_Bakery.prefab", "Bakery", new Vector3(31f, 0f, -24f), Quaternion.Euler(0f, -90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Building);
        CreatePerceivablePrefab(parent, BuildingPrefabFolder + "Building_Books Shop.prefab", "Book_Shop", new Vector3(-31f, 0f, 18f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Building);
        CreatePerceivablePrefab(parent, BuildingPrefabFolder + "Building_Super Market.prefab", "Super_Market", new Vector3(31f, 0f, 24f), Quaternion.Euler(0f, -90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Building);
        CreatePerceivablePrefab(parent, BuildingPrefabFolder + "Building_House_01_color01.prefab", "House_West", new Vector3(-31f, 0f, 43f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Building);
        CreatePerceivablePrefab(parent, BuildingPrefabFolder + "Building_House_02_color02.prefab", "House_East", new Vector3(31f, 0f, 43f), Quaternion.Euler(0f, -90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Building);
    }

    private static void CreateStreetPropPrefabs(Transform parent, int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        CreatePerceivablePrefab(parent, PropPrefabFolder + "Props_Bus Stop.prefab", "Bus_Stop", new Vector3(SidewalkX - 2.4f, 0f, 28f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Prop);
        CreatePerceivablePrefab(parent, PropPrefabFolder + "Props_Bench_1.prefab", "Bench", new Vector3(SidewalkX - 2.2f, 0f, -16f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Prop);
        CreatePerceivablePrefab(parent, PropPrefabFolder + "Props_Dustbin.prefab", "Dustbin", new Vector3(SidewalkX + 1.4f, 0f, -5f), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Prop);
        CreatePerceivablePrefab(parent, PropPrefabFolder + "Props_Street Light.prefab", "Street_Light_South", new Vector3(SidewalkX + 1.6f, 0f, -29f), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Prop);
        CreatePerceivablePrefab(parent, PropPrefabFolder + "Props_Street Light.prefab", "Street_Light_North", new Vector3(SidewalkX + 1.6f, 0f, 25f), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Prop);
        CreatePerceivablePrefab(parent, PropPrefabFolder + "Props_Traffic Signal_big.prefab", "Traffic_Signal", new Vector3(SidewalkX + 2.5f, 0f, 1.6f), Quaternion.Euler(0f, 90f, 0f), Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Prop);
        CreatePerceivablePrefab(parent, PropPrefabFolder + "Props_Traffic cone.prefab", "Traffic_Cone", new Vector3(-4f, 0f, 12f), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Prop);
    }

    private static void CreateTactilePavingPrefabs(Transform parent, int hiddenLayer, int revealLayer, Material revealMaterial)
    {
        for (int i = 0; i <= 78; i++)
        {
            float z = -39f + i;
            CreatePerceivablePrefab(parent, PropPrefabFolder + "Yellow_Tactile_Paving_Tile.prefab", "Tactile_Guide_Tile_" + i, new Vector3(SidewalkX, 0.02f, z), Quaternion.identity, Vector3.one, hiddenLayer, revealLayer, revealMaterial, SurfaceProfile.Tactile);
        }
    }

    private static void CreateVehiclePrefabs(Transform parent, int revealLayer, Material revealMaterial)
    {
        CreateMovingVehiclePrefab(parent, "Vehicle_Car_color01.prefab", "Traffic_Car_01", new Vector3(-3.2f, 0.05f, -48f), new Vector3(-3.2f, 0.05f, 48f), 5.4f, 0f, revealLayer, revealMaterial);
        CreateMovingVehiclePrefab(parent, "Vehicle_Car_color02.prefab", "Traffic_Car_02", new Vector3(3.2f, 0.05f, 48f), new Vector3(3.2f, 0.05f, -48f), 5.8f, 0.18f, revealLayer, revealMaterial);
        CreateMovingVehiclePrefab(parent, "Vehicle_Car_color03.prefab", "Traffic_Car_03", new Vector3(-48f, 0.05f, -3.2f), new Vector3(48f, 0.05f, -3.2f), 5.1f, 0.36f, revealLayer, revealMaterial);
        CreateMovingVehiclePrefab(parent, "Vehicle_Taxi.prefab", "Traffic_Taxi", new Vector3(48f, 0.05f, 3.2f), new Vector3(-48f, 0.05f, 3.2f), 5.7f, 0.54f, revealLayer, revealMaterial);
        CreateMovingVehiclePrefab(parent, "Vehicle_SUV_color01.prefab", "Traffic_SUV", new Vector3(-3.2f, 0.05f, -48f), new Vector3(-3.2f, 0.05f, 48f), 4.9f, 0.68f, revealLayer, revealMaterial);
        CreateMovingVehiclePrefab(parent, "Vehicle_Pick up Truck_color01.prefab", "Traffic_Pickup", new Vector3(3.2f, 0.05f, 48f), new Vector3(3.2f, 0.05f, -48f), 4.6f, 0.82f, revealLayer, revealMaterial);
        CreateMovingVehiclePrefab(parent, "Vehicle_Bus_color01.prefab", "Traffic_Bus", new Vector3(-48f, 0.05f, -3.2f), new Vector3(48f, 0.05f, -3.2f), 4.2f, 0.14f, revealLayer, revealMaterial);
        CreateMovingVehiclePrefab(parent, "Vehicle_Truck_color01.prefab", "Traffic_Truck", new Vector3(48f, 0.05f, 3.2f), new Vector3(-48f, 0.05f, 3.2f), 4.1f, 0.72f, revealLayer, revealMaterial);
    }

    private static void CreateMovingVehiclePrefab(
        Transform parent,
        string prefabName,
        string objectName,
        Vector3 laneStart,
        Vector3 laneEnd,
        float speed,
        float initialProgress,
        int revealLayer,
        Material revealMaterial)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePrefabFolder + prefabName);
        if (prefab == null)
        {
            Debug.LogWarning("Missing SimplePoly vehicle prefab: " + VehiclePrefabFolder + prefabName);
            return;
        }

        GameObject vehicle = InstantiatePrefab(prefab, objectName, parent, laneStart, Quaternion.identity, Vector3.one);
        SetLayerRecursively(vehicle, revealLayer);
        AssignMaterialRecursively(vehicle, revealMaterial);
        EnsureUsefulCollider(vehicle);
        ConfigurePerceivable(vehicle, SurfaceProfile.Vehicle);

        SimplePolyTrafficVehicleMover mover = vehicle.AddComponent<SimplePolyTrafficVehicleMover>();
        mover.laneStart = laneStart;
        mover.laneEnd = laneEnd;
        mover.speed = speed;
        mover.initialProgress = initialProgress;
        mover.modelYawOffset = 0f;
    }

    private static Transform CreateChildRoot(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void CreatePerceivablePrefab(
        Transform parent,
        string prefabPath,
        string objectName,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        int hiddenLayer,
        int revealLayer,
        Material revealMaterial,
        SurfaceProfile surfaceProfile)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("Missing SimplePoly City prefab: " + prefabPath);
            return;
        }

        GameObject hidden = InstantiatePrefab(prefab, objectName + "_HiddenNormalColour", parent, position, rotation, scale);
        SetLayerRecursively(hidden, hiddenLayer);
        RemoveColliders(hidden);

        GameObject reveal = InstantiatePrefab(prefab, objectName + "_PerceptionReveal", parent, position, rotation, scale);
        SetLayerRecursively(reveal, revealLayer);
        AssignMaterialRecursively(reveal, revealMaterial);
        EnsureUsefulCollider(reveal);
        ConfigurePerceivable(reveal, surfaceProfile);
    }

    private static GameObject InstantiatePrefab(GameObject prefab, string name, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.rotation = rotation;
        instance.transform.localScale = scale;
        return instance;
    }

    private static void ConfigurePerceivable(GameObject target, SurfaceProfile surfaceProfile)
    {
        PerceivableRevealObject perceivable = target.GetComponent<PerceivableRevealObject>();
        if (perceivable == null)
        {
            perceivable = target.AddComponent<PerceivableRevealObject>();
        }

        perceivable.defaultRevealRadius = surfaceProfile.revealRadius;
        perceivable.fadeOutSeconds = 0.3f;
        perceivable.minimumPointSpacing = 0.08f;
        perceivable.maxRevealPoints = 32;
        perceivable.includeChildRenderers = true;
        perceivable.surfacePattern = surfaceProfile.pattern;
        perceivable.surfaceShapeStrength = surfaceProfile.surfaceStrength;
        perceivable.surfaceShapeSpacing = surfaceProfile.surfaceSpacing;
        perceivable.surfaceShapeWidth = surfaceProfile.surfaceWidth;
        perceivable.surfaceFill = surfaceProfile.surfaceFill;
    }

    private static void AssignMaterialRecursively(GameObject root, Material material)
    {
        if (material == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                materials[materialIndex] = material;
            }

            renderers[i].sharedMaterials = materials;
        }
    }

    private static void EnsureUsefulCollider(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            return;
        }

        Bounds? rendererBounds = GetRendererBounds(root);
        if (!rendererBounds.HasValue)
        {
            return;
        }

        BoxCollider boxCollider = root.AddComponent<BoxCollider>();
        Bounds bounds = rendererBounds.Value;
        boxCollider.center = root.transform.InverseTransformPoint(bounds.center);
        boxCollider.size = GetLocalBoundsSize(root.transform, bounds.size);
    }

    private static Bounds? GetRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return null;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Vector3 GetLocalBoundsSize(Transform rootTransform, Vector3 worldSize)
    {
        Vector3 lossyScale = rootTransform.lossyScale;
        float x = Mathf.Approximately(lossyScale.x, 0f) ? worldSize.x : worldSize.x / Mathf.Abs(lossyScale.x);
        float y = Mathf.Approximately(lossyScale.y, 0f) ? worldSize.y : worldSize.y / Mathf.Abs(lossyScale.y);
        float z = Mathf.Approximately(lossyScale.z, 0f) ? worldSize.z : worldSize.z / Mathf.Abs(lossyScale.z);
        return new Vector3(Mathf.Max(x, 0.05f), Mathf.Max(y, 0.05f), Mathf.Max(z, 0.05f));
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            children[i].gameObject.layer = layer;
        }
    }

    private static void RemoveColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = colliders.Length - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(colliders[i]);
        }
    }

    private static Material LoadRevealMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RevealMaterialPath);
        if (material == null)
        {
            Debug.LogError("Missing reveal material at " + RevealMaterialPath);
        }

        return material;
    }

    private static Material LoadCaneMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CaneMaterialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("BlindPerception/Contact Reveal Lines URP");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, CaneMaterialPath);
        return material;
    }

    private static void ConfigureLighting()
    {
        Light light = Object.FindFirstObjectByType<Light>();
        if (light == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            light = lightObject.AddComponent<Light>();
        }

        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.transform.position = new Vector3(0f, 8f, 0f);
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
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

        Debug.LogWarning("Layer " + layerIndex + " is already named '" + layer.stringValue + "'. The city test scene expected '" + layerName + "'.");
    }

    private readonly struct SurfaceProfile
    {
        public readonly float revealRadius;
        public readonly PerceivableRevealObject.SurfaceShapePattern pattern;
        public readonly float surfaceStrength;
        public readonly float surfaceSpacing;
        public readonly float surfaceWidth;
        public readonly float surfaceFill;

        private SurfaceProfile(float revealRadius, PerceivableRevealObject.SurfaceShapePattern pattern, float surfaceStrength, float surfaceSpacing, float surfaceWidth, float surfaceFill)
        {
            this.revealRadius = revealRadius;
            this.pattern = pattern;
            this.surfaceStrength = surfaceStrength;
            this.surfaceSpacing = surfaceSpacing;
            this.surfaceWidth = surfaceWidth;
            this.surfaceFill = surfaceFill;
        }

        public static SurfaceProfile Road => new SurfaceProfile(0.72f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.08f, 0.24f, 0.025f, 0.06f);
        public static SurfaceProfile Sidewalk => new SurfaceProfile(0.64f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.1f, 0.22f, 0.025f, 0.08f);
        public static SurfaceProfile RoadMarking => new SurfaceProfile(0.45f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.18f, 0.18f, 0.025f, 0.12f);
        public static SurfaceProfile Building => new SurfaceProfile(0.72f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.1f, 0.22f, 0.025f, 0.08f);
        public static SurfaceProfile Prop => new SurfaceProfile(0.58f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.16f, 0.2f, 0.025f, 0.08f);
        public static SurfaceProfile Tactile => new SurfaceProfile(0.72f, PerceivableRevealObject.SurfaceShapePattern.DirectionalStrips, 1f, 0.16f, 0.035f, 0.1f);
        public static SurfaceProfile Vehicle => new SurfaceProfile(0.75f, PerceivableRevealObject.SurfaceShapePattern.PlainSurface, 0.12f, 0.2f, 0.025f, 0.08f);
    }
}
