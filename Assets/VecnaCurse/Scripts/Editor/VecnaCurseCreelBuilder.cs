using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Milestone 3: builds the Creel House exterior prefab from the reduced model made by
/// Tools/Blender/creel_reduce.py. The house door faces -Z and its lowest point sits at y = 0.
/// Safe to run more than once.
/// </summary>
public static class VecnaCurseCreelBuilder
{
    const string ModelPath = "Assets/VecnaCurse/Environments/CreelHouse/CreelHouse.fbx";
    const string TextureFolder = "Assets/Save The Hawkins/Vecna's Curse/Haunted Creel House/textures/";
    const string MaterialFolder = "Assets/VecnaCurse/Materials/CreelHouse";
    const string PrefabPath = "Assets/VecnaCurse/Prefabs/VC_Env_CreelHouseExterior.prefab";
    const string DoorMarkerName = "CreelHouse_DoorMarker";
    const string SceneInstanceName = "VC_CreelHouse_Exterior";
    const string VecnaSceneGuid = "1a6c1a3dc077547c782db1ad2f309ed5";

    // Main walls measured in Blender: the wall runs back from the door marker.
    const float BodyWidth = 12.4f;
    const float BodyDepth = 9.8f;
    const float BodyHeight = 15.9f;

    static readonly Vector3 ScenePosition = new Vector3(0f, 0f, 15f);
    static readonly float[] LodTransitions = { 0.4f, 0.15f, 0.03f };

    struct Surface
    {
        public string Material, BaseMap, NormalMap;
        public Color Tint;

        public Surface(string material, string baseMap, string normalMap)
        {
            Material = material;
            BaseMap = baseMap;
            NormalMap = normalMap;
            Tint = Color.white;
        }

        public Surface(string material, Color tint)
        {
            Material = material;
            BaseMap = null;
            NormalMap = null;
            Tint = tint;
        }
    }

    static readonly Surface[] Surfaces =
    {
        new Surface("Base", "base_diff.png", "base_norm_copy.png"),
        new Surface("Roof", "roof_diff.png", "roof_norm.png"),
        new Surface("Floor", "floor_diff.png", "floor_norm.png"),
        new Surface("Windows", "windows_diff.png", "windows_norm.png"),
        new Surface("Deco", "deco_diff_g.png", "deco_norm.png"),
        new Surface("Balustrade14", "balustrade14_diff.png", "balustrade14_norm.png"),
        new Surface("Columns", "columns_diff.png", "columns_norm.png"),
        new Surface("Columns2", "columns_2_diff.png", "columns_2_norm.png"),
        // Opaque dark glass: no transparency cost, and it hides the hollow shell.
        new Surface("Glass", new Color(0.035f, 0.045f, 0.055f)),
        new Surface("Black", new Color(0.02f, 0.02f, 0.02f))
    };

    [MenuItem("Save The Hawkins/Vecna Curse/Run Milestone 3 Creel House Build")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[VecnaCurse] Exit Play Mode before building the Creel House.");
            return;
        }

        // Also applies the door yaw fix from Milestone 2.
        VecnaCursePropBuilder.Run();

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[VecnaCurse] Creel House build cancelled.");
            return;
        }

        if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer))
        {
            Debug.LogError($"[VecnaCurse] Creel House model not found at {ModelPath}. Focus Unity so it imports, then retry.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/VecnaCurse/Materials", "CreelHouse");

        importer.importCameras = false;
        importer.importLights = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        foreach (Surface surface in Surfaces)
        {
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), surface.Material),
                GetOrCreateMaterial(surface));
        }
        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();

        GameObject prefab = BuildPrefab();
        if (prefab == null) return;

        PlaceInScene(prefab);
        Debug.Log("[VecnaCurse] Milestone 3 Creel House build finished.");
    }

    static GameObject BuildPrefab()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var root = new GameObject("VC_Env_CreelHouseExterior");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);

            Transform marker = FindChild(instance.transform, DoorMarkerName);
            if (marker == null)
            {
                Debug.LogError($"[VecnaCurse] {DoorMarkerName} is missing from the model.");
                return null;
            }

            // Turn the house so the door side faces -Z, whichever way the FBX axes came in.
            Vector3 toDoor = marker.position;
            toDoor.y = 0f;
            float yaw = Vector3.SignedAngle(toDoor.normalized, Vector3.back, Vector3.up);
            instance.transform.Rotate(Vector3.up, yaw, Space.World);

            LODGroup lodGroup = instance.GetComponentInChildren<LODGroup>();
            if (lodGroup == null)
            {
                Debug.LogError("[VecnaCurse] No LODGroup on the Creel House. Check the _LOD0 mesh names.");
                return null;
            }

            LOD[] lods = lodGroup.GetLODs();
            string summary = "";
            for (int i = 0; i < lods.Length && i < LodTransitions.Length; i++)
            {
                lods[i].screenRelativeTransitionHeight = LodTransitions[i];
                int triangles = 0;
                foreach (Renderer r in lods[i].renderers)
                {
                    if (r.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    {
                        for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                            triangles += (int)filter.sharedMesh.GetIndexCount(s) / 3;
                    }
                }
                summary += $" LOD{i}={triangles}";
            }
            lodGroup.SetLODs(lods);

            Vector3 door = marker.position;
            var body = root.AddComponent<BoxCollider>();
            body.center = new Vector3(door.x, BodyHeight * 0.5f, door.z + BodyDepth * 0.5f);
            body.size = new Vector3(BodyWidth, BodyHeight, BodyDepth);

            Bounds bounds = lods[0].renderers[0].bounds;
            Debug.Log($"[VecnaCurse] CreelHouse:{summary} triangles; size {bounds.size:F2}; " +
                      $"lowest y {bounds.min.y:F2}; door marker {door:F2}; yaw {yaw:F1}.");

            StaticEditorFlags flags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void PlaceInScene(GameObject prefab)
    {
        Scene scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(VecnaSceneGuid), OpenSceneMode.Single);
        foreach (GameObject existing in scene.GetRootGameObjects())
        {
            if (existing.name == SceneInstanceName) Object.DestroyImmediate(existing);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = SceneInstanceName;
        instance.transform.SetPositionAndRotation(ScenePosition, Quaternion.identity);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static Material GetOrCreateMaterial(Surface surface)
    {
        string path = $"{MaterialFolder}/VC_Creel_{surface.Material}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", surface.Tint);
        if (surface.BaseMap != null)
            material.SetTexture("_BaseMap", GetTexture(surface.BaseMap, 1024, false));
        if (surface.NormalMap != null)
        {
            material.SetTexture("_BumpMap", GetTexture(surface.NormalMap, 512, true));
            material.EnableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture2D GetTexture(string fileName, int maxSize, bool normalMap)
    {
        string path = TextureFolder + fileName;
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
        {
            Debug.LogError($"[VecnaCurse] Texture not found: {path}");
            return null;
        }

        importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.mipmapEnabled = true;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = maxSize,
            format = normalMap ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_8x8,
            compressionQuality = (int)TextureCompressionQuality.Normal
        });
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }
}
