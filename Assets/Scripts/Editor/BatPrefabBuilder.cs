using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds Assets/Resources/Bat.prefab from the bat FBX: looping flap animation,
/// URP materials with the bat textures, size normalised, head facing +Z,
/// trigger collider (so bullets can hit it) and the BatFlyer script.
/// Runs once automatically when the prefab is missing; rebuild from the menu.
/// </summary>
[InitializeOnLoad]
public static class BatPrefabBuilder
{
    const string FbxPath = "Assets/Prefab/bat/source/bat_animflapping.fbx";
    const string BodyTexPath = "Assets/Prefab/bat/textures/Murcielago_body.jpeg";
    const string EyeTexPath = "Assets/Prefab/bat/textures/Murcielago_ojo.jpeg";
    const string ControllerPath = "Assets/Prefab/bat/BatAnimator.controller";
    const string BodyMatPath = "Assets/Prefab/bat/BatBody.mat";
    const string EyeMatPath = "Assets/Prefab/bat/BatEye.mat";
    const string PrefabPath = "Assets/Resources/Bat.prefab";
    const float Wingspan = 0.7f;

    static BatPrefabBuilder()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) != null)
                Build();
        };
    }

    [MenuItem("Save The Hawkins/Build Bat Prefab")]
    public static void Build()
    {
        // 1. Make the flap clip loop.
        var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        if (importer == null)
        {
            Debug.LogError("BatPrefabBuilder: FBX not found at " + FbxPath);
            return;
        }
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool changed = false;
        foreach (var c in clips)
        {
            if (!c.loopTime) { c.loopTime = true; changed = true; }
        }
        if (changed || importer.clipAnimations.Length == 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(a => !a.name.StartsWith("__preview__"));

        // 2. Animator controller with the flap as default state.
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null && clip != null)
            controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);

        // 3. URP materials using the bat textures.
        Material bodyMat = GetOrCreateMaterial(BodyMatPath, BodyTexPath, 0.15f);
        Material eyeMat = GetOrCreateMaterial(EyeMatPath, EyeTexPath, 0.8f);

        // 4. Assemble the prefab.
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var root = new GameObject("Bat");
        var model = (GameObject)Object.Instantiate(fbx);
        model.name = "Model";
        model.transform.SetParent(root.transform, false);

        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            int eyeSlot = FindEyeSlot(r, mats);
            for (int i = 0; i < mats.Length; i++)
                mats[i] = i == eyeSlot ? eyeMat : bodyMat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // cheap on Quest
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
        }

        // Face the head along +Z, using the eye bones.
        Bounds b = GetBounds(model);
        Transform eye = model.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "Eyes" || t.name == "Eye_L" || t.name == "Eye_R");
        Vector3 headDir = eye != null ? eye.position - b.center : Vector3.zero;
        headDir.y = 0f;
        if (headDir.sqrMagnitude < 1e-6f)
            headDir = b.size.x > b.size.z ? Vector3.forward : Vector3.right; // fallback: wings across X
        model.transform.rotation = Quaternion.FromToRotation(headDir.normalized, Vector3.forward) * model.transform.rotation;

        // Normalise size to a fixed wingspan and centre on the root.
        b = GetBounds(model);
        float span = Mathf.Max(b.size.x, b.size.z);
        if (span > 1e-4f)
            model.transform.localScale *= Wingspan / span;
        b = GetBounds(model);
        model.transform.position -= b.center;

        Animator anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = Wingspan * 0.45f;

        root.AddComponent<BatFlyer>();
        root.tag = "Enemy"; // lets VRGun fire when bats are in range

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Debug.Log("BatPrefabBuilder: built " + PrefabPath + (clip != null ? " with clip " + clip.name : " (no animation clip found)"));
    }

    static int FindEyeSlot(Renderer r, Material[] mats)
    {
        for (int i = 0; i < mats.Length; i++)
        {
            string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
            if (n.Contains("ojo") || n.Contains("eye")) return i;
        }
        // Fallback: with two slots, the smaller submesh is the eyes.
        Mesh mesh = null;
        if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
        else if (r.TryGetComponent(out MeshFilter mf)) mesh = mf.sharedMesh;
        if (mesh != null && mats.Length == 2 && mesh.subMeshCount == 2)
            return mesh.GetIndexCount(0) < mesh.GetIndexCount(1) ? 0 : 1;
        return -1;
    }

    static Material GetOrCreateMaterial(string path, string texPath, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
        }
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        mat.enableInstancing = true;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }
}
