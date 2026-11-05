using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Milestone 1 setup for Vecna's Curse. Safe to run more than once: every step
/// checks the current state first. Assets are found by GUID so folder moves do not break it.
/// </summary>
public static class VecnaCurseSceneSetup
{
    const string HomeSceneGuid = "bd8dd264d30054a159774a42da8a10ef";
    const string CitySceneGuid = "01bfb0bab466d469984e43f7de211cd3";
    const string VecnaSceneGuid = "1a6c1a3dc077547c782db1ad2f309ed5";
    const string XrRigPrefabGuid = "f6336ac4ac8b4d34bc5072418cdc62a0";

    const string VecnaSceneName = "Vecna Curse";
    const string RootFolder = "Assets/VecnaCurse";
    const string GroundMaterialPath = RootFolder + "/Materials/VC_Placeholder_Ground.mat";
    const string SpawnName = "VC_PlayerSpawn";
    const string GroundName = "TEMP_SpawnGround_Placeholder";

    static readonly string[] Folders =
    {
        "Scripts", "Scripts/Core", "Scripts/Player", "Scripts/Weapons", "Scripts/Enemies",
        "Scripts/Boss", "Scripts/Gameplay", "Scripts/Missions", "Scripts/Puzzles",
        "Scripts/Interactions", "Scripts/UI", "Scripts/Audio", "Scripts/VFX",
        "Models", "Characters", "Environments", "Props", "Materials", "Textures",
        "Animations", "Prefabs", "Audio", "VFX"
    };

    [MenuItem("Save The Hawkins/Vecna Curse/Run Milestone 1 Setup")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[VecnaCurse] Exit Play Mode before running setup.");
            return;
        }

        // Keeps unsaved work: the user can save or cancel before any scene is replaced.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[VecnaCurse] Setup cancelled. Nothing changed.");
            return;
        }

        CreateFolders();

        string vecnaPath = RenameVecnaScene();
        if (vecnaPath == null) return;

        if (!WireHomeButton()) return;
        UpdateBuildSettings(vecnaPath);
        SetUpVecnaScene(vecnaPath);

        Debug.Log("[VecnaCurse] Milestone 1 setup finished.");
    }

    static void CreateFolders()
    {
        if (!AssetDatabase.IsValidFolder(RootFolder))
            AssetDatabase.CreateFolder("Assets", "VecnaCurse");

        foreach (string folder in Folders)
        {
            string path = RootFolder + "/" + folder;
            if (AssetDatabase.IsValidFolder(path)) continue;

            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }

    static string RenameVecnaScene()
    {
        string path = AssetDatabase.GUIDToAssetPath(VecnaSceneGuid);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[VecnaCurse] Vecna scene not found by GUID.");
            return null;
        }

        if (System.IO.Path.GetFileNameWithoutExtension(path) != VecnaSceneName)
        {
            string error = AssetDatabase.RenameAsset(path, VecnaSceneName);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("[VecnaCurse] Scene rename failed: " + error);
                return null;
            }
        }
        return AssetDatabase.GUIDToAssetPath(VecnaSceneGuid);
    }

    static bool WireHomeButton()
    {
        Scene home = EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(HomeSceneGuid), OpenSceneMode.Single);

        Transform tile = null;
        Button demoButton = null;
        foreach (GameObject root in home.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Vecna's Curse" || t.parent == null) continue;
                Transform demo = t.parent.Find("Demo Hunt");
                if (demo == null || !demo.TryGetComponent(out demoButton)) continue;
                tile = t;
                break;
            }
            if (tile != null) break;
        }

        if (tile == null)
        {
            Debug.LogError("[VecnaCurse] Could not find the Vecna's Curse tile beside Demo Hunt in Home.");
            return false;
        }

        StartGame startGame = demoButton.onClick.GetPersistentEventCount() > 0
            ? demoButton.onClick.GetPersistentTarget(0) as StartGame
            : null;
        if (startGame == null)
        {
            Debug.LogError("[VecnaCurse] Demo Hunt button does not target StartGame.");
            return false;
        }

        if (!tile.TryGetComponent(out Button button))
        {
            button = tile.gameObject.AddComponent<Button>();
            EditorUtility.CopySerialized(demoButton, button); // Same hover and press feedback as Demo Hunt.
        }
        button.targetGraphic = tile.GetComponent<Graphic>();

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, startGame.OnVecnaCurseClick);
        EditorUtility.SetDirty(button);

        // "Wait" is the unavailable overlay on the tile.
        Transform wait = tile.Find("Wait");
        if (wait != null) wait.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(home);
        EditorSceneManager.SaveScene(home);
        return true;
    }

    static void UpdateBuildSettings(string vecnaPath)
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(AssetDatabase.GUIDToAssetPath(HomeSceneGuid), true),
            new EditorBuildSettingsScene(AssetDatabase.GUIDToAssetPath(CitySceneGuid), true),
            new EditorBuildSettingsScene(vecnaPath, true)
        };
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void SetUpVecnaScene(string vecnaPath)
    {
        Scene scene = EditorSceneManager.OpenScene(vecnaPath, OpenSceneMode.Single);

        // The rig brings its own camera and listener, so the scene's default camera must go.
        XROrigin origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Main Camera" && root.GetComponent<XROrigin>() == null)
                Object.DestroyImmediate(root);
        }

        Transform spawn = GetOrCreateRoot(scene, SpawnName).transform;
        spawn.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        if (origin == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(XrRigPrefabGuid));
            if (prefab == null)
            {
                Debug.LogError("[VecnaCurse] XR Origin prefab not found by GUID.");
                return;
            }
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            rig.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            origin = rig.GetComponent<XROrigin>();
        }
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        Camera cam = origin.Camera;
        cam.farClipPlane = 200f; // Same as Hawkin City.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.015f, 0.02f);
        EditorUtility.SetDirty(cam);
        EditorUtility.SetDirty(origin);

        if (Object.FindFirstObjectByType<XRInteractionManager>(FindObjectsInactive.Include) == null)
            GetOrCreateRoot(scene, "XR Interaction Manager").AddComponent<XRInteractionManager>();

        if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            GameObject events = GetOrCreateRoot(scene, "EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<XRUIInputModule>();
        }

        SetUpPlaceholderGround(scene);
        SetUpMoodLighting(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        int rigs = Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        int eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        string counts = $"XROrigin={rigs}, AudioListener={listeners}, EventSystem={eventSystems}";
        if (rigs == 1 && listeners == 1 && eventSystems == 1)
            Debug.Log("[VecnaCurse] Scene checks passed: " + counts);
        else
            Debug.LogError("[VecnaCurse] Scene checks failed, expected one of each: " + counts);
    }

    static void SetUpPlaceholderGround(Scene scene)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            material.SetColor("_BaseColor", new Color(0.12f, 0.11f, 0.11f));
            AssetDatabase.CreateAsset(material, GroundMaterialPath);
        }

        GameObject ground = FindRoot(scene, GroundName);
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = GroundName;
            SceneManager.MoveGameObjectToScene(ground, scene);
        }
        ground.transform.SetPositionAndRotation(new Vector3(0f, -0.5f, 0f), Quaternion.identity);
        ground.transform.localScale = new Vector3(60f, 1f, 60f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static void SetUpMoodLighting(Scene scene)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.16f, 0.13f, 0.15f);

        GameObject lightObject = FindRoot(scene, "Directional Light");
        if (lightObject != null && lightObject.TryGetComponent(out Light sun))
        {
            sun.color = new Color(0.62f, 0.68f, 0.85f);
            sun.intensity = 0.5f;
            sun.shadows = LightShadows.None;
            EditorUtility.SetDirty(sun);
        }
    }

    static GameObject GetOrCreateRoot(Scene scene, string name)
    {
        GameObject found = FindRoot(scene, name);
        if (found != null) return found;

        var created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, scene);
        return created;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
        }
        return null;
    }
}
