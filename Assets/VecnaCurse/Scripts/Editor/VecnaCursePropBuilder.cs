using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Milestone 2: configures small prop imports for Quest and builds their prefabs.
/// Safe to run more than once. Source files stay where they were downloaded.
/// </summary>
public static class VecnaCursePropBuilder
{
    const string SourceRoot = "Assets/Save The Hawkins/Vecna's Curse/";
    const string MaterialFolder = "Assets/VecnaCurse/Materials/Props";
    const string PrefabFolder = "Assets/VecnaCurse/Prefabs/Props";
    const string VecnaSceneGuid = "1a6c1a3dc077547c782db1ad2f309ed5";
    const string LineupName = "TEMP_PropLineup";
    const string MaterialPrefix = "VC_Prop_";

    class Slot
    {
        public string Name;
        public string BaseMap;
        public string NormalMap;
        public string EmissionMap;
        public Color Tint = Color.white;
    }

    class Prop
    {
        public string Name;
        public string Fbx;
        public float LongestSizeMeters;
        public int MaxTextureSize;
        public bool PivotAtFloor;
        public bool ExpectTall;
        public float ModelYaw; // Turns the model so its broad face points along Z.
        public Slot[] Slots;
        public Slot UnnamedSlot; // For an FBX with no materials of its own.
        public string[] HiddenParts = new string[0];
    }

    static readonly Prop[] Props =
    {
        new Prop
        {
            Name = "LockedDoor", Fbx = "Locked door/source/Door.fbx",
            LongestSizeMeters = 2.05f, MaxTextureSize = 1024, PivotAtFloor = true, ExpectTall = true, ModelYaw = 90f,
            Slots = new[]
            {
                new Slot
                {
                    Name = "Default",
                    BaseMap = "Locked door/textures/Door_Default_Albedo.png",
                    NormalMap = "Locked door/textures/Door_Default_Normal.png"
                }
            }
        },
        new Prop
        {
            Name = "WallClock", Fbx = "wall-clock/source/WallClock_LP/WallClock_LP.fbx",
            LongestSizeMeters = 0.45f, MaxTextureSize = 512,
            HiddenParts = new[] { "Glass_LP" }, // Avoids a transparent draw on a small prop.
            Slots = new[]
            {
                new Slot
                {
                    Name = "Clock",
                    BaseMap = "wall-clock/source/WallClock_LP/WallClock_LP_Clock_BaseMap.png",
                    NormalMap = "wall-clock/source/WallClock_LP/WallClock_LP_Clock_Normal.png"
                },
                new Slot { Name = "Glass", Tint = new Color(0.5f, 0.5f, 0.5f) },
                new Slot { Name = "Black_Pointer", Tint = new Color(0.03f, 0.03f, 0.03f) },
                new Slot { Name = "Red_Pointer", Tint = new Color(0.6f, 0.03f, 0.03f) }
            }
        },
        new Prop
        {
            Name = "Flashlight", Fbx = "Flashlight/source/Flashlight_low.fbx",
            LongestSizeMeters = 0.24f, MaxTextureSize = 512,
            Slots = new[]
            {
                new Slot
                {
                    Name = "Flashlight",
                    BaseMap = "Flashlight/textures/Flashlight_Base_Color.png",
                    NormalMap = "Flashlight/textures/Flashlight_Normal_OpenGL.png",
                    EmissionMap = "Flashlight/textures/Flashlight_Emissive.png"
                }
            }
        },
        new Prop
        {
            Name = "CRTTelevision", Fbx = "CRT Television/source/old tv.fbx",
            LongestSizeMeters = 0.6f, MaxTextureSize = 1024, PivotAtFloor = true,
            Slots = new[]
            {
                new Slot { Name = "wood", BaseMap = "CRT Television/textures/TexturesCom_Wood_Wenge_1K_albedo.png" },
                new Slot { Name = "back", BaseMap = "CRT Television/textures/OLD TV TEX.jpeg" },
                new Slot { Name = "screenReflect", BaseMap = "CRT Television/textures/OLD TV TEX.jpeg" }
            }
        },
        new Prop
        {
            Name = "GrandfatherClock", Fbx = "Grandfather Clock/Vecna's Clock.fbx",
            LongestSizeMeters = 2.2f, PivotAtFloor = true, ExpectTall = true,
            Slots = new Slot[0],
            UnnamedSlot = new Slot { Name = "GrandfatherClock", Tint = new Color(0.18f, 0.09f, 0.05f) }
        },
        new Prop
        {
            Name = "CassettePlayer", Fbx = "Radio and Cassette Player/source/beril_sony_walkman_3/beril_sony_walkman_3.fbx",
            LongestSizeMeters = 0.25f, MaxTextureSize = 512,
            HiddenParts = new[] { "sony-text", "walkman_text" }, // Removes third-party brand text.
            Slots = new[]
            {
                new Slot { Name = "Material", BaseMap = "Radio and Cassette Player/source/beril_sony_walkman_3/colors.jpg" },
                new Slot { Name = "Material.001", BaseMap = "Radio and Cassette Player/source/beril_sony_walkman_3/colors_2.jpg" }
            }
        }
    };

    [MenuItem("Save The Hawkins/Vecna Curse/Run Milestone 2 Prop Build")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[VecnaCurse] Exit Play Mode before building props.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[VecnaCurse] Prop build cancelled. Nothing changed.");
            return;
        }

        CreateFolder("Assets/VecnaCurse/Materials", "Props");
        CreateFolder("Assets/VecnaCurse/Prefabs", "Props");

        var prefabs = new List<GameObject>();
        int failures = 0;
        foreach (Prop prop in Props)
        {
            GameObject prefab = BuildProp(prop);
            if (prefab != null) prefabs.Add(prefab);
            else failures++;
        }

        AssetDatabase.SaveAssets();
        PlaceLineup(prefabs);

        if (failures == 0)
            Debug.Log($"[VecnaCurse] Milestone 2 prop build finished: {prefabs.Count} prefabs.");
        else
            Debug.LogError($"[VecnaCurse] Prop build finished with {failures} failures.");
    }

    static GameObject BuildProp(Prop prop)
    {
        string fbxPath = SourceRoot + prop.Fbx;
        if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter importer))
        {
            Debug.LogError($"[VecnaCurse] {prop.Name}: model not found at {fbxPath}");
            return null;
        }

        importer.importCameras = false;
        importer.importLights = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Low;

        foreach (Slot slot in prop.Slots)
        {
            Material material = GetOrCreateMaterial(prop, slot);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot.Name), material);
        }
        importer.SaveAndReimport();

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var root = new GameObject("VC_Prop_" + prop.Name);
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.transform.localRotation = Quaternion.Euler(0f, prop.ModelYaw, 0f) * instance.transform.localRotation;

            foreach (Transform part in instance.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(prop.HiddenParts, part.name) >= 0)
                    part.gameObject.SetActive(false);
            }

            if (prop.UnnamedSlot != null)
            {
                Material material = GetOrCreateMaterial(prop, prop.UnnamedSlot);
                foreach (MeshRenderer r in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    r.sharedMaterials = materials;
                }
            }

            if (!TryGetBounds(instance, out Bounds raw))
            {
                Debug.LogError($"[VecnaCurse] {prop.Name}: no visible mesh.");
                return null;
            }

            float longest = Mathf.Max(raw.size.x, raw.size.y, raw.size.z);
            instance.transform.localScale *= prop.LongestSizeMeters / longest;

            TryGetBounds(instance, out Bounds scaled);
            Vector3 pivot = prop.PivotAtFloor
                ? new Vector3(scaled.center.x, scaled.min.y, scaled.center.z)
                : scaled.center;
            instance.transform.position -= pivot;
            TryGetBounds(instance, out Bounds final);

            var box = root.AddComponent<BoxCollider>();
            box.center = final.center;
            box.size = final.size;

            if (prop.ExpectTall && final.size.y < prop.LongestSizeMeters * 0.99f)
                Debug.LogWarning($"[VecnaCurse] {prop.Name}: height is not the longest axis. Check orientation.");

            int triangles = CountTriangles(instance);
            Debug.Log($"[VecnaCurse] {prop.Name}: raw {raw.size:F3}, final {final.size:F3} m, {triangles} triangles.");

            return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/VC_Prop_{prop.Name}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static Material GetOrCreateMaterial(Prop prop, Slot slot)
    {
        string slotPart = slot.Name == prop.Name ? "" : "_" + slot.Name.Replace(".", "");
        string path = $"{MaterialFolder}/{MaterialPrefix}{prop.Name}{slotPart}.mat";

        // Simple Lit without specular keeps prop shading cheap on Quest.
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", slot.Tint);

        if (slot.BaseMap != null)
            material.SetTexture("_BaseMap", GetTexture(slot.BaseMap, prop.MaxTextureSize, false));

        if (slot.NormalMap != null)
        {
            material.SetTexture("_BumpMap", GetTexture(slot.NormalMap, prop.MaxTextureSize, true));
            material.EnableKeyword("_NORMALMAP");
        }

        if (slot.EmissionMap != null)
        {
            material.SetTexture("_EmissionMap", GetTexture(slot.EmissionMap, prop.MaxTextureSize, false));
            material.SetColor("_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    static Texture2D GetTexture(string relativePath, int maxSize, bool normalMap)
    {
        string path = SourceRoot + relativePath;
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
            format = TextureImporterFormat.ASTC_6x6,
            compressionQuality = (int)TextureCompressionQuality.Normal
        });
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>Temporary row in the Vecna Curse scene so scale and orientation can be checked.</summary>
    static void PlaceLineup(List<GameObject> prefabs)
    {
        Scene scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(VecnaSceneGuid), OpenSceneMode.Single);

        foreach (GameObject existing in scene.GetRootGameObjects())
        {
            if (existing.name == LineupName) Object.DestroyImmediate(existing);
        }

        var lineup = new GameObject(LineupName);
        SceneManager.MoveGameObjectToScene(lineup, scene);

        const float spacing = 1.5f;
        float x = -spacing * (prefabs.Count - 1) * 0.5f;
        foreach (GameObject prefab in prefabs)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, lineup.transform);
            bool floorPivot = instance.GetComponent<BoxCollider>().center.y > 0.01f;
            instance.transform.localPosition = new Vector3(x, floorPivot ? 0f : 1.2f, 3f);
            instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            x += spacing;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static bool TryGetBounds(GameObject instance, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(false))
        {
            if (!any)
            {
                bounds = r.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }
        return any;
    }

    static int CountTriangles(GameObject instance)
    {
        int count = 0;
        foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(false))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            for (int i = 0; i < mesh.subMeshCount; i++)
                count += (int)mesh.GetIndexCount(i) / 3;
        }
        return count;
    }

    static void CreateFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
