using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Milestone 4: builds the Hawkins street in the Vecna Curse scene: terrain mesh, night skybox,
/// fog, road, sidewalks, houses, streetlights, dead trees and play bounds. Models come from
/// Tools/Blender/hawkins_env.py. Safe to run more than once: VC_Hawkins is rebuilt each time.
/// </summary>
public static class VecnaCurseHawkinsBuilder
{
    const string VecnaSceneGuid = "1a6c1a3dc077547c782db1ad2f309ed5";
    const string RootName = "VC_Hawkins";
    const string EnvFolder = "Assets/VecnaCurse/Environments/Hawkins";
    const string MaterialFolder = "Assets/VecnaCurse/Materials/Hawkins";
    const string PrefabFolder = "Assets/VecnaCurse/Prefabs/Hawkins";
    const string SkyTexturePath = "Assets/VecnaCurse/Textures/VC_Sky_HawkinsNight.png";
    const string PoolTexturePath = "Assets/VecnaCurse/Textures/VC_LightPool.png";
    const string RoadTextures = "Assets/Save The Hawkins/Hawkins City Assets/american-road/textures/";
    const string HouseTextures = "Assets/Save The Hawkins/Hawkins City Assets/american-house/textures/";
    const string SimpleLit = "Universal Render Pipeline/Simple Lit";

    // Street layout, in metres. The player spawns at the origin looking along +Z.
    const float RoadHalfWidth = 4f;
    const float SidewalkWidth = 2f;
    const float StreetStartZ = -20f;
    const float StreetEndZ = 118f;
    const float PathEndZ = 131f;
    const float PlayHalfWidth = 24f;
    const float PlayStartZ = -22f;
    const float PlayEndZ = 165f;
    const float HouseOffsetX = 15f;
    const float StreetLightOffsetX = 5.2f;
    const int TreeCount = 34;

    static readonly float[] HouseRowsZ = { 25f, 50f, 75f };
    static readonly float[] StreetLightRowsZ = { 8f, 32f, 56f, 80f, 104f };

    static readonly Color FogColor = new Color(0.04f, 0.026f, 0.045f).gamma;

    [MenuItem("Save The Hawkins/Vecna Curse/Run Milestone 4 Hawkins Build")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[VecnaCurse] Exit Play Mode before building Hawkins.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[VecnaCurse] Hawkins build cancelled. Nothing changed.");
            return;
        }

        CreateFolder("Assets/VecnaCurse/Materials", "Hawkins");
        CreateFolder("Assets/VecnaCurse/Prefabs", "Hawkins");

        Material skybox = BuildSkybox();
        GameObject housePrefab = BuildHousePrefab();
        GameObject lightPrefab = BuildStreetLightPrefab();
        List<GameObject> treePrefabs = BuildTreePrefabs();
        if (skybox == null || housePrefab == null || lightPrefab == null || treePrefabs.Count == 0) return;

        Scene scene = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(VecnaSceneGuid), OpenSceneMode.Single);
        RemoveRoot(scene, RootName);
        RemoveRoot(scene, "TEMP_SpawnGround_Placeholder");
        RemoveRoot(scene, "TEMP_PropLineup");

        var root = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        BuildGround(root.transform);
        PlaceHouses(root.transform, housePrefab);
        PlaceStreetLights(root.transform, lightPrefab);
        PlaceTrees(root.transform, treePrefabs);
        BuildBounds(root.transform);
        ApplyAtmosphere(scene, skybox);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[VecnaCurse] Milestone 4 Hawkins build finished.");
    }

    // ---------- Sky and atmosphere ----------

    static Material BuildSkybox()
    {
        if (!(AssetImporter.GetAtPath(SkyTexturePath) is TextureImporter importer))
        {
            Debug.LogError($"[VecnaCurse] Sky texture not found: {SkyTexturePath}");
            return null;
        }
        importer.textureShape = TextureImporterShape.TextureCube;
        importer.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        SetAndroidFormat(importer, 1024, TextureImporterFormat.ASTC_6x6);
        importer.SaveAndReimport();

        Material material = GetMaterial($"{MaterialFolder}/VC_Sky_HawkinsNight.mat", "Skybox/Cubemap");
        material.SetTexture("_Tex", AssetDatabase.LoadAssetAtPath<Cubemap>(SkyTexturePath));
        EditorUtility.SetDirty(material);
        return material;
    }

    static void ApplyAtmosphere(Scene scene, Material skybox)
    {
        RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.31f, 0.38f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogStartDistance = 12f;
        RenderSettings.fogEndDistance = 135f;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Directional Light" && root.TryGetComponent(out Light moon))
            {
                moon.color = new Color(0.62f, 0.7f, 0.9f);
                moon.intensity = 0.9f;
                moon.shadows = LightShadows.None;
                root.transform.rotation = Quaternion.Euler(38f, -35f + 180f, 0f); // Matches the moon in the sky texture.
                EditorUtility.SetDirty(moon);
            }

            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(camera);
            }
        }
    }

    // ---------- Ground ----------

    static void BuildGround(Transform parent)
    {
        Material grass = LitMaterial("VC_Hawkins_DeadGrass", RoadTextures + "Grass02.jpg", 512, new Color(0.75f, 0.66f, 0.5f));
        Material road = LitMaterial("VC_Hawkins_Road", RoadTextures + "RoadLines.jpg", 1024, new Color(0.65f, 0.65f, 0.65f));
        Material sidewalk = LitMaterial("VC_Hawkins_Sidewalk", RoadTextures + "Sidewalk01.jpg", 512, new Color(0.5f, 0.48f, 0.46f));
        Material dirt = LitMaterial("VC_Hawkins_DirtPath", RoadTextures + "Sidewalk01.jpg", 512, new Color(0.2f, 0.16f, 0.13f));

        Mesh terrain = SaveMesh(BuildTerrainMesh(), "VC_Hawkins_TerrainMesh");
        GameObject terrainObject = AddMeshObject(parent, "VC_Terrain", terrain, grass);
        terrainObject.AddComponent<MeshCollider>().sharedMesh = terrain;
        terrainObject.isStatic = true;

        float sideInner = RoadHalfWidth;
        float sideOuter = RoadHalfWidth + SidewalkWidth;
        AddStrip(parent, "VC_Road", road, -RoadHalfWidth, RoadHalfWidth, StreetStartZ, StreetEndZ, 0.02f, 8f, 1f);
        AddStrip(parent, "VC_SidewalkLeft", sidewalk, -sideOuter, -sideInner, StreetStartZ, StreetEndZ, 0.03f, 2f, 1f);
        AddStrip(parent, "VC_SidewalkRight", sidewalk, sideInner, sideOuter, StreetStartZ, StreetEndZ, 0.03f, 2f, 1f);
        AddStrip(parent, "VC_PathToCreelHouse", dirt, -2f, 2f, StreetEndZ, PathEndZ, 0.03f, 4f, 1f);
    }

    static Mesh BuildTerrainMesh()
    {
        const int cells = 60;
        const float cellSize = 6f;
        const float minX = -180f;
        const float minZ = -80f;

        var vertices = new Vector3[(cells + 1) * (cells + 1)];
        var uvs = new Vector2[vertices.Length];
        for (int z = 0; z <= cells; z++)
        {
            for (int x = 0; x <= cells; x++)
            {
                float wx = minX + x * cellSize;
                float wz = minZ + z * cellSize;
                int i = z * (cells + 1) + x;
                vertices[i] = new Vector3(wx, TerrainHeight(wx, wz), wz);
                uvs[i] = new Vector2(wx, wz) / 8f;
            }
        }

        var triangles = new int[cells * cells * 6];
        for (int z = 0, t = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                int a = z * (cells + 1) + x;
                int b = a + cells + 1;
                triangles[t++] = a;
                triangles[t++] = b;
                triangles[t++] = a + 1;
                triangles[t++] = a + 1;
                triangles[t++] = b;
                triangles[t++] = b + 1;
            }
        }

        var mesh = new Mesh { vertices = vertices, uv = uvs, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Flat through the playable area, rising into low hills that fade into fog.</summary>
    static float TerrainHeight(float x, float z)
    {
        float outsideX = Mathf.Max(0f, Mathf.Abs(x) - (PlayHalfWidth + 2f));
        float outsideZ = Mathf.Max(0f, Mathf.Max(PlayStartZ - 2f - z, z - (PlayEndZ + 2f)));
        float distance = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ);
        float rise = Mathf.SmoothStep(0f, 1f, distance / 70f);
        return rise * (5f + 14f * Mathf.PerlinNoise(x * 0.018f + 40f, z * 0.018f + 40f));
    }

    static void AddStrip(Transform parent, string name, Material material, float minX, float maxX, float minZ, float maxZ,
        float y, float tileLength, float uWidthTiles)
    {
        const float segmentLength = 10f;
        int segments = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / segmentLength));
        var vertices = new Vector3[(segments + 1) * 2];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[segments * 6];
        for (int s = 0; s <= segments; s++)
        {
            float z = Mathf.Lerp(minZ, maxZ, (float)s / segments);
            vertices[s * 2] = new Vector3(minX, y, z);
            vertices[s * 2 + 1] = new Vector3(maxX, y, z);
            float v = (z - minZ) / tileLength;
            uvs[s * 2] = new Vector2(0f, v);
            uvs[s * 2 + 1] = new Vector2(uWidthTiles, v);
        }
        for (int s = 0, t = 0; s < segments; s++)
        {
            int a = s * 2;
            triangles[t++] = a;
            triangles[t++] = a + 2;
            triangles[t++] = a + 1;
            triangles[t++] = a + 1;
            triangles[t++] = a + 2;
            triangles[t++] = a + 3;
        }

        var mesh = new Mesh { vertices = vertices, uv = uvs, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AddMeshObject(parent, name, SaveMesh(mesh, name + "Mesh"), material).isStatic = true;
    }

    // ---------- Prefabs ----------

    static GameObject BuildHousePrefab()
    {
        string path = EnvFolder + "/AmericanHouse.fbx";
        Material main = LitMaterial("VC_Hawkins_House", HouseTextures + "american_house_DIFF.png", 1024, new Color(0.62f, 0.6f, 0.6f));
        Material glass = ColourMaterial("VC_Hawkins_HouseGlass", new Color(0.03f, 0.04f, 0.05f));
        if (!ConfigureModel(path, new Dictionary<string, Material> { { "Main", main }, { "Objects", main }, { "Glass", glass } }))
            return null;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = new GameObject("VC_Env_AmericanHouse");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            LODGroup lodGroup = instance.GetComponentInChildren<LODGroup>();
            if (lodGroup == null)
            {
                Debug.LogError("[VecnaCurse] No LODGroup on the house. Check the _LOD0 mesh names.");
                return null;
            }

            LOD[] lods = lodGroup.GetLODs();
            lods[0].screenRelativeTransitionHeight = 0.3f;
            lods[1].screenRelativeTransitionHeight = 0.03f;
            lodGroup.SetLODs(lods);

            Bounds bounds = lods[0].renderers[0].bounds;
            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            Debug.Log($"[VecnaCurse] House size {bounds.size:F2}, lowest y {bounds.min.y:F2}.");

            return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/VC_Env_AmericanHouse.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static GameObject BuildStreetLightPrefab()
    {
        string path = EnvFolder + "/StreetLight.fbx";
        Material metal = ColourMaterial("VC_Hawkins_StreetMetal", new Color(0.09f, 0.09f, 0.1f));
        Material lamp = UnlitMaterial("VC_Hawkins_LampGlow", new Color(1f, 0.72f, 0.38f));
        if (!ConfigureModel(path, new Dictionary<string, Material> { { "VC_StreetMetal", metal }, { "VC_LampGlow", lamp } }))
            return null;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = new GameObject("VC_Env_StreetLight");
        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            Bounds bounds = instance.GetComponentInChildren<Renderer>().bounds;

            // Point the arm along +Z so scene placement only needs a yaw toward the road.
            Vector3 arm = bounds.center;
            arm.y = 0f;
            instance.transform.Rotate(Vector3.up, Vector3.SignedAngle(arm, Vector3.forward, Vector3.up), Space.World);

            var pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(pool.GetComponent<Collider>());
            pool.name = "VC_LightPool";
            pool.transform.SetParent(root.transform, false);
            pool.transform.localPosition = new Vector3(0f, 0.08f, arm.magnitude * 2f);
            pool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pool.transform.localScale = new Vector3(13f, 13f, 1f);
            pool.GetComponent<MeshRenderer>().sharedMaterial = LightPoolMaterial();

            var pole = root.AddComponent<CapsuleCollider>();
            pole.center = new Vector3(0f, 3f, 0f);
            pole.radius = 0.15f;
            pole.height = 6f;

            return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/VC_Env_StreetLight.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static List<GameObject> BuildTreePrefabs()
    {
        var prefabs = new List<GameObject>();
        string path = EnvFolder + "/DeadTrees.fbx";
        Material bark = ColourMaterial("VC_Hawkins_DeadBark", new Color(0.075f, 0.06f, 0.05f));
        if (!ConfigureModel(path, new Dictionary<string, Material> { { "VC_DeadBark", bark } })) return prefabs;

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (!(asset is Mesh mesh)) continue;

            var root = new GameObject("VC_Env_" + mesh.name);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = bark;
                var trunk = root.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0f, 1.5f, 0f);
                trunk.radius = 0.4f;
                trunk.height = 3f;
                prefabs.Add(PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/VC_Env_{mesh.name}.prefab"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        return prefabs;
    }

    // ---------- Placement ----------

    static void PlaceHouses(Transform parent, GameObject prefab)
    {
        var group = NewGroup(parent, "VC_Houses");
        foreach (float z in HouseRowsZ)
        {
            Place(prefab, group, new Vector3(-HouseOffsetX, 0f, z), 90f); // The porch side is the model's +Z.
            Place(prefab, group, new Vector3(HouseOffsetX, 0f, z), -90f);
        }
    }

    static void PlaceStreetLights(Transform parent, GameObject prefab)
    {
        var group = NewGroup(parent, "VC_StreetLights");
        for (int i = 0; i < StreetLightRowsZ.Length; i++)
        {
            bool left = i % 2 == 0;
            float x = left ? -StreetLightOffsetX : StreetLightOffsetX;
            Place(prefab, group, new Vector3(x, 0f, StreetLightRowsZ[i]), left ? 90f : -90f);
        }
    }

    static void PlaceTrees(Transform parent, List<GameObject> prefabs)
    {
        var group = NewGroup(parent, "VC_DeadTrees");
        var random = new System.Random(1986);
        int placed = 0;
        for (int attempt = 0; attempt < TreeCount * 20 && placed < TreeCount; attempt++)
        {
            float side = random.NextDouble() < 0.5 ? -1f : 1f;
            float x = side * Mathf.Lerp(RoadHalfWidth + SidewalkWidth + 2f, 75f, (float)random.NextDouble());
            float z = Mathf.Lerp(PlayStartZ, PlayEndZ, (float)random.NextDouble());
            if (!TreeFits(x, z)) continue;

            GameObject prefab = prefabs[placed % prefabs.Count];
            GameObject tree = Place(prefab, group, new Vector3(x, TerrainHeight(x, z) - 0.05f, z), (float)random.NextDouble() * 360f);
            tree.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.25f, (float)random.NextDouble());
            placed++;
        }
    }

    static bool TreeFits(float x, float z)
    {
        foreach (float rowZ in HouseRowsZ)
        {
            if (Mathf.Abs(Mathf.Abs(x) - HouseOffsetX) < 11f && Mathf.Abs(z - rowZ) < 11f) return false;
        }
        bool nearCreelHouse = Mathf.Abs(x) < 14f && z > StreetEndZ - 4f;
        bool nearSpawn = Mathf.Abs(x) < 10f && z < 8f;
        return !nearCreelHouse && !nearSpawn;
    }

    static void BuildBounds(Transform parent)
    {
        var group = NewGroup(parent, "VC_PlayBounds");
        float length = PlayEndZ - PlayStartZ;
        float midZ = (PlayEndZ + PlayStartZ) * 0.5f;
        AddWall(group, "Left", new Vector3(-PlayHalfWidth, 5f, midZ), new Vector3(1f, 10f, length));
        AddWall(group, "Right", new Vector3(PlayHalfWidth, 5f, midZ), new Vector3(1f, 10f, length));
        AddWall(group, "Back", new Vector3(0f, 5f, PlayStartZ), new Vector3(PlayHalfWidth * 2f, 10f, 1f));
        AddWall(group, "Far", new Vector3(0f, 5f, PlayEndZ), new Vector3(PlayHalfWidth * 2f, 10f, 1f));
    }

    static void AddWall(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = position;
        wall.AddComponent<BoxCollider>().size = size;
    }

    // ---------- Helpers ----------

    static GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return instance;
    }

    static Transform NewGroup(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    static GameObject AddMeshObject(Transform parent, string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = $"{EnvFolder}/{name}.asset";
        mesh.name = name;
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    static bool ConfigureModel(string path, Dictionary<string, Material> materials)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
        {
            Debug.LogError($"[VecnaCurse] Model not found: {path}. Run Tools/Blender/hawkins_env.py first.");
            return false;
        }
        importer.importCameras = false;
        importer.importLights = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        foreach (KeyValuePair<string, Material> pair in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
        importer.SaveAndReimport();
        return true;
    }

    static Material LitMaterial(string name, string texturePath, int maxSize, Color tint)
    {
        Material material = GetMaterial($"{MaterialFolder}/{name}.mat", SimpleLit);
        if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
        {
            SetAndroidFormat(importer, maxSize, TextureImporterFormat.ASTC_8x8);
            importer.SaveAndReimport();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        }
        else
        {
            Debug.LogError($"[VecnaCurse] Texture not found: {texturePath}");
        }
        material.SetColor("_BaseColor", tint);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material ColourMaterial(string name, Color colour)
    {
        Material material = GetMaterial($"{MaterialFolder}/{name}.mat", SimpleLit);
        material.SetColor("_BaseColor", colour);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material UnlitMaterial(string name, Color colour)
    {
        Material material = GetMaterial($"{MaterialFolder}/{name}.mat", "Universal Render Pipeline/Unlit");
        material.SetColor("_BaseColor", colour);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>Soft additive glow on the ground. Additional lights are disabled in the project's URP asset.</summary>
    static Material LightPoolMaterial()
    {
        if (!File.Exists(PoolTexturePath))
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float value = falloff * falloff;
                    texture.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }
            File.WriteAllBytes(PoolTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PoolTexturePath);
        }

        if (AssetImporter.GetAtPath(PoolTexturePath) is TextureImporter importer)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            SetAndroidFormat(importer, 128, TextureImporterFormat.ASTC_6x6);
            importer.SaveAndReimport();
        }

        Material material = GetMaterial($"{MaterialFolder}/VC_Hawkins_LightPool.mat", "Universal Render Pipeline/Particles/Unlit");
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PoolTexturePath));
        material.SetColor("_BaseColor", new Color(0.75f, 0.5f, 0.25f, 1f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    static Material GetMaterial(string path, string shaderName)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    static void SetAndroidFormat(TextureImporter importer, int maxSize, TextureImporterFormat format)
    {
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = maxSize,
            format = format,
            compressionQuality = (int)TextureCompressionQuality.Normal
        });
    }

    static void RemoveRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) Object.DestroyImmediate(root);
        }
    }

    static void CreateFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
