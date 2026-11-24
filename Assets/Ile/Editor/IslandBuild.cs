using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using cowsins;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class IslandBuild : IProcessSceneWithReport
{
    public const string ScenePath = "Assets/Scenes/Jeu_Ile.unity";
    private const string Coast = "Assets/NatureManufacture Assets/Coast Environment/Demo Scene/Coast Demo.unity";
    private const string Movement = "Assets/Cowsins/Demo/MovementShowroom.unity";
    private const string Showroom = "Assets/Cowsins/Demo/Showroom.unity";

    [MenuItem("Ile/Assembler la scene")]
    public static void Assemble()
    {
        if (File.Exists(ScenePath))
            throw new InvalidOperationException($"Scene already exists: {ScenePath}");

        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        AssembleFromAssets();
    }

    private static void AssembleFromAssets()
    {
        UpgradeMaterials();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        renderer.renderingMode = RenderingMode.Forward;
        if (!renderer.rendererFeatures.Any(feature => feature is DecalRendererFeature))
        {
            var decals = ScriptableObject.CreateInstance<DecalRendererFeature>();
            decals.name = "Coast Decals";
            AssetDatabase.AddObjectToAsset(decals, renderer);
            renderer.rendererFeatures.Add(decals);
        }
        var serializedRenderer = new SerializedObject(renderer);
        var decalFeature = renderer.rendererFeatures.OfType<DecalRendererFeature>().Single();
        var decalsSettings = new SerializedObject(decalFeature);
        decalsSettings.FindProperty("m_Settings.technique").intValue = 2;
        decalsSettings.FindProperty("m_Settings.maxDrawDistance").floatValue = 200;
        decalsSettings.ApplyModifiedPropertiesWithoutUndo();
        var featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        featureMap.arraySize = renderer.rendererFeatures.Count;
        for (int i = 0; i < featureMap.arraySize; i++)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out _, out long identifier);
            featureMap.GetArrayElementAtIndex(i).longValue = identifier;
        }
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();

        var island = EditorSceneManager.OpenScene(Coast);
        var oldCamera = Root(island, "Main Camera");
        Vector3 reference = oldCamera.transform.position;
        Object.DestroyImmediate(oldCamera);
        var terrain = Root(island, "Terrain (1)").GetComponent<Terrain>();
        Vector3 spawn = FindSpawn(terrain, reference);

        var movement = EditorSceneManager.OpenScene(Movement, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(island);
        var controller = Root(movement, "MovementCowsinsFPSController");
        SceneManager.MoveGameObjectToScene(controller, island);
        controller.transform.SetPositionAndRotation(spawn + Vector3.up * 0.2f, Quaternion.identity);
        var weaponController = controller.GetComponentInChildren<WeaponController>(true);
        var pistol = AssetDatabase.LoadAssetAtPath<Weapon_SO>("Assets/Cowsins/ScriptableObjects/Weapons/Pistol.asset");
        if (!weaponController || !pistol)
            throw new InvalidOperationException("The movement controller and pistol from FPS Engine are required.");
        weaponController.settings.initialWeapons = new[] { pistol };
        PrefabUtility.RecordPrefabInstancePropertyModifications(weaponController);
        SceneManager.MoveGameObjectToScene(Root(movement, "EventSystem"), island);

        var obstacles = new GameObject("Parcours de mouvement").transform;
        var movementEnvironment = Root(movement, "Environment");
        var platforms = movementEnvironment.GetComponentsInChildren<Transform>()
            .Where(item => item.name == "Platform" && item.GetComponentsInChildren<Collider>().Length > 0)
            .OrderBy(item => item.position.y)
            .Take(4).ToArray();
        if (platforms.Length < 4)
            throw new InvalidOperationException("The movement showroom must provide four platforms.");
        Place(obstacles, platforms[0].gameObject, terrain, spawn + new Vector3(8, 0, 8), 0);
        Place(obstacles, platforms[1].gameObject, terrain, spawn + new Vector3(8, 0, 13), 0.5f);
        Place(obstacles, platforms[2].gameObject, terrain, spawn + new Vector3(8, 0, 19), 1f);
        Place(obstacles, platforms[3].gameObject, terrain, spawn + new Vector3(8, 0, 26), 1.5f);
        EditorSceneManager.CloseScene(movement, true);

        var showroom = EditorSceneManager.OpenScene(Showroom, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(island);
        var shootingRange = new GameObject("Stand de tir").transform;
        var target = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cowsins/Prefabs/Others/CircularTargetEnemy.prefab");
        for (int i = 0; i < 3; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(target, shootingRange);
            Vector3 point = spawn + new Vector3(-10 - i * 3, 0, 12 + i * 5);
            Vector3 facing = new Vector3(spawn.x - point.x, 0, spawn.z - point.z);
            instance.transform.SetPositionAndRotation(OnTerrain(terrain, point) + Vector3.up, Quaternion.LookRotation(facing));
        }
        var objects = Root(showroom, "Environment").GetComponentsInChildren<Transform>();
        var props = objects.FirstOrDefault(item => item.name == "PropCubes");
        if (props)
            Place(shootingRange, props.gameObject, terrain, spawn + new Vector3(-10, 0, 7), 0);
        EditorSceneManager.CloseScene(showroom, true);

        EditorSceneManager.MarkSceneDirty(island);
        if (!EditorSceneManager.SaveScene(island, ScenePath))
            throw new IOException($"Could not save {ScenePath}");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log($"Island assembled at {spawn}.");
    }

    private static GameObject Root(Scene scene, string name) =>
        scene.GetRootGameObjects().Single(root => root.name == name);

    private static Vector3 FindSpawn(Terrain terrain, Vector3 reference)
    {
        Vector3 best = default;
        float bestScore = float.PositiveInfinity;
        for (int x = 290; x <= 650; x += 5)
        for (int z = 450; z <= 790; z += 5)
        {
            Vector3 point = OnTerrain(terrain, new Vector3(x, 0, z));
            if (point.y < 23 || point.y > 65)
                continue;
            float slope = 0;
            for (int dx = -18; dx <= 18; dx += 6)
            for (int dz = 0; dz <= 30; dz += 6)
                slope = Mathf.Max(slope, Mathf.Abs(OnTerrain(terrain, point + new Vector3(dx, 0, dz)).y - point.y));
            if (Physics.CheckCapsule(point + Vector3.up * 0.6f, point + Vector3.up * 1.7f, 0.35f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                continue;
            float score = slope * 12 + Vector2.Distance(new Vector2(x, z), new Vector2(reference.x, reference.z)) * 0.08f;
            if (score < bestScore)
            {
                best = point;
                bestScore = score;
            }
        }
        if (float.IsPositiveInfinity(bestScore))
            throw new InvalidOperationException("Could not find a clear spawn above sea level.");
        return best;
    }

    private static Vector3 OnTerrain(Terrain terrain, Vector3 point)
    {
        point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
        return point;
    }

    private static void Place(Transform parent, GameObject template, Terrain terrain, Vector3 destination, float elevation)
    {
        var item = Object.Instantiate(template, parent);
        item.name = template.name;
        item.transform.rotation = template.transform.rotation;
        item.transform.localScale = template.transform.lossyScale;
        item.transform.position = OnTerrain(terrain, destination);
        Physics.SyncTransforms();
        var colliders = item.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
            item.transform.position += Vector3.up * (item.transform.position.y - colliders.Min(collider => collider.bounds.min.y) + elevation);
    }

    [MenuItem("Ile/Construire WebGL")]
    public static void BuildWeb()
    {
        if (!File.Exists(ScenePath))
            AssembleFromAssets();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new BuildFailedException("Install Unity Web Build Support for this editor version.");

        UpgradeMaterials();
        ConfigureWebTextures();
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        var output = Environment.GetEnvironmentVariable("ISLAND_WEBGL_OUTPUT") ?? "Builds/WebGL";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            target = BuildTarget.WebGL,
            locationPathName = output,
            options = BuildOptions.StrictMode
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException($"WebGL build {report.summary.result}: {report.summary.totalErrors} errors.");
        Debug.Log($"WebGL build succeeded: {report.summary.totalSize:N0} bytes in {output}.");
    }

    public static void BuildFromSource()
    {
        AssembleFromAssets();
        BuildWeb();
    }

    private static void UpgradeMaterials()
    {
        if (AssetDatabase.FindAssets("t:Material", new[] { "Assets/Cowsins" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))
            .Any(material => material && material.shader && material.shader.name == "Standard"))
        {
            Converters.RunInBatchMode("BuiltInToURP", new List<string> { "Material" }, isInclusive: true);
            AssetDatabase.SaveAssets();
        }
    }

    private static void ConfigureWebTextures()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in AssetDatabase.GetDependencies(ScenePath))
            {
                if (!path.StartsWith("Assets/NatureManufacture Assets/", StringComparison.Ordinal) ||
                    AssetImporter.GetAtPath(path) is not TextureImporter texture)
                    continue;

                int limit = texture.textureType == TextureImporterType.Lightmap ? 1024 : 512;
                var settings = texture.GetPlatformTextureSettings("WebGL");
                int currentSize = settings.overridden ? settings.maxTextureSize : texture.maxTextureSize;
                if (currentSize <= limit)
                    continue;
                settings.overridden = true;
                settings.maxTextureSize = limit;
                texture.SetPlatformTextureSettings(settings);
                texture.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report?.summary.platform != BuildTarget.WebGL || scene.path != ScenePath)
            return;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name is "VFX" or "Waterfall VFX" or "Cliff Fog" or "ProbeVolumePerSceneData")
                Object.DestroyImmediate(root);
    }
}
