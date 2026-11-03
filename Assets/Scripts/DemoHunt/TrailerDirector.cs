using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 60-second FIRST-PERSON trailer, shot in-engine (editor only).
/// Home menu -> step through the mirror -> Hawkin City: Demogorgons emerge, charge, pounce;
/// the player shoots and slashes; bats; a far-off gate beam and a lightning-lit glimpse of
/// something huge (teased, not explained). Title cards use the Benguiat font.
/// Started by Save The Hawkins > Record 1-Minute Trailer (saves MP4 to the Desktop).
/// </summary>
public class TrailerDirector : MonoBehaviour
{
    public const string CameraTag = "TrailerCam";
    public const string SessionKey = "STH_Trailer";
    public static bool Active { get; private set; }
    /// <summary>True when the shot list has ended (recorder stops then).</summary>
    public static bool Finished { get; private set; }
    static TrailerDirector instance;

    // Camera rig
    Camera cam;
    Transform camT, gunModel, swordPivot;
    Vector3 basePos, lookPoint, muzzleLocal;
    bool walking;
    float bobTime, recoil, shake, lookSpeed = 4f;
    Quaternion swordRest;

    // Cards
    Renderer cardBg;
    TextMeshPro cardTitle, cardSub, caption;
    TMP_FontAsset font;

    // Assets / scene refs
    GameObject bulletPrefab;
    AudioClip gunshot, heartbeat, spawnClip;
    EnemySpawner spawner;
    Light sun;
    Color sunColor;
    float sunIntensity;
    bool cityReady;
    bool armed; // rifle + sword only appear once in Hawkin City

    // Cinematic grade refs (pulsed on hits) + score
    LensDistortion lens;
    ChromaticAberration chroma;
    ColorAdjustments grade;
    AudioSource droneSrc, arpSrc;
    UniversalRenderPipelineAsset rpAsset;
    float oldRenderScale, oldShadowDist;
    int oldMsaa;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance == null && UnityEditor.SessionState.GetBool(SessionKey, false))
            new GameObject("Trailer Director").AddComponent<TrailerDirector>();
    }

    static T Load<T>(string path) where T : Object => UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
    static T Load<T>(string path) where T : Object => null;
#endif

    void Awake()
    {
        instance = this;
        Active = true;
        Finished = false;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;

        font = Load<TMP_FontAsset>("Assets/Fonts/Benguiat Bold SDF.asset");
        bulletPrefab = Load<GameObject>("Assets/Prefab/Bullet.prefab");
        heartbeat = Load<AudioClip>("Assets/Audio/Heart Beat.mp3");
        spawnClip = Load<AudioClip>("Assets/Audio/demogorgon_Spawn.mp3");
        DemogorgonAI.AttackClip = Load<AudioClip>("Assets/Audio/demogorgon_Attack.mp3");
        BoostQuality();
        BuildCamera();
        BuildScore();
    }

    // Full resolution, 4x MSAA, long shadows while recording (restored afterwards).
    void BoostQuality()
    {
        rpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (rpAsset == null) return;
        oldRenderScale = rpAsset.renderScale;
        oldMsaa = rpAsset.msaaSampleCount;
        oldShadowDist = rpAsset.shadowDistance;
        rpAsset.renderScale = 1f;
        rpAsset.msaaSampleCount = 4;
        rpAsset.shadowDistance = 90f;
        QualitySettings.lodBias = 3f;
    }

    void BuildScore()
    {
        droneSrc = MusicSource(TrailerAudio.Drone);
        arpSrc = MusicSource(TrailerAudio.Arp);
    }

    AudioSource MusicSource(AudioClip c)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = c;
        src.loop = true;
        src.volume = 0f;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
        src.Play();
        return src;
    }

    IEnumerator Fade(AudioSource src, float to, float t)
    {
        float from = src.volume;
        for (float e = 0f; e < t; e += Time.unscaledDeltaTime) { src.volume = Mathf.Lerp(from, to, e / t); yield return null; }
        src.volume = to;
    }

    void Music(AudioSource src, float to, float t) => StartCoroutine(Fade(src, to, t));
    static void Sfx(AudioClip c, float vol = 1f, float pitch = 1f) => GameFx.Play2D(c, vol, pitch);

    // Hit: BRAAM/impact + lens punch, chromatic burst, exposure kick, shake.
    void Hit(float strength = 1f, bool braam = false)
    {
        Sfx(TrailerAudio.Impact, 0.9f * strength);
        if (braam) Sfx(TrailerAudio.Braam, 0.85f);
        shake = Mathf.Max(shake, 0.6f * strength);
        StartCoroutine(Punch(strength));
    }

    IEnumerator Punch(float strength)
    {
        for (float e = 0f; e < 0.5f; e += Time.unscaledDeltaTime)
        {
            float k = 1f - e / 0.5f;
            k *= k;
            if (lens != null) lens.intensity.value = -0.38f * strength * k;
            if (chroma != null) chroma.intensity.value = 0.15f + 0.85f * strength * k;
            if (grade != null) grade.postExposure.value = 0.35f + 0.9f * strength * k;
            yield return null;
        }
        if (lens != null) lens.intensity.value = 0f;
        if (chroma != null) chroma.intensity.value = 0.15f;
        if (grade != null) grade.postExposure.value = 0.35f;
    }

    void OnDestroy()
    {
        Active = false;
        Time.timeScale = 1f;
        if (rpAsset != null)
        {
            rpAsset.renderScale = oldRenderScale;
            rpAsset.msaaSampleCount = oldMsaa;
            rpAsset.shadowDistance = oldShadowDist;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    IEnumerator Start()
    {
        yield return null;
        yield return Run();
    }

    // ===================================================================
    //  SHOT LIST (~60 s)
    // ===================================================================
    IEnumerator Run()
    {
        // ---------------- HOME (0:00 - 0:12) ----------------
        StartGame sg = FindFirstObjectByType<StartGame>();
        Invoke(sg, "ShowMainMenu");
        Transform title = Field<GameObject>(sg, "gameTitle")?.transform;
        Transform gate = Field<GameObject>(sg, "upsideDownGate")?.transform;

        Vector3 eye = Eye();
        basePos = eye;
        lookPoint = title != null ? title.position : eye + Vector3.forward * 3f;
        camT.SetPositionAndRotation(eye, Quaternion.LookRotation(lookPoint - eye));
        SetCard(1f, "", "");
        Music(droneSrc, 0.55f, 4f);
        Sfx(TrailerAudio.Impact, 0.5f, 0.7f);
        yield return FadeFromBlack(1.5f);
        Sfx(TrailerAudio.Ting, 0.35f);
        Caption("HAWKINS, INDIANA  -  1986", 3.5f);
        yield return new WaitForSeconds(3f);

        // Menu: Start -> Demo Hunt; the mirror wakes up.
        sg.StartButton();
        yield return new WaitForSeconds(1.2f);
        sg.OnDemoHuntClick();
        GameFx.Play2D(heartbeat, 0.8f, 0.9f);
        if (gate == null) gate = Field<GameObject>(sg, "upsideDownGate")?.transform;
        Vector3 mirror = gate != null ? GateCenter(gate) : eye + Vector3.forward * 4f;
        var glow = GameFx.CreateLight(gate != null ? gate : transform, Vector3.zero, new Color(1f, 0.1f, 0.05f), 6f, 0f);
        glow.transform.position = mirror;

        lookSpeed = 2.5f;
        lookPoint = mirror;
        yield return new WaitForSeconds(1.3f);
        // Walk to the mirror; it glows red, heartbeat speeds up.
        Vector3 stop = mirror + (eye - mirror).normalized * 0.9f;
        stop.y = eye.y;
        StartCoroutine(Ramp(v => glow.intensity = v * 6f, 3.5f));
        Sfx(TrailerAudio.Riser(3.75f), 0.8f);
        yield return MoveTo(stop, 3.5f);
        shake = 0.6f;
        Sfx(TrailerAudio.Whoosh, 0.9f, 0.8f);
        yield return Flash(Color.white, 0.25f);
        Hit(1.2f, true);
        sg.EnterUpsideDown(); // load Hawkin City (camera persists)
        yield return new WaitUntil(() => cityReady);

        // ---------------- CITY (0:12 - 0:47) ----------------
        armed = true;
        Vector3 P = Eye();
        basePos = P;
        Vector3 fwd = Flat(Camera.main != null ? Camera.main.transform.forward : Vector3.forward);
        lookPoint = P + fwd * 10f + Vector3.up * 2f;
        camT.SetPositionAndRotation(P, Quaternion.LookRotation(lookPoint - P));
        SetCard(1f, "", "");
        StartCoroutine(Lightning(1.2f));
        arpSrc.time = 0f;
        Music(arpSrc, 0.35f, 3f);
        yield return FadeFromBlack(1f);

        // Slow look around the infested street.
        lookSpeed = 1.2f;
        lookPoint = P + Quaternion.Euler(0f, -60f, 0f) * fwd * 12f + Vector3.up * 3f;
        StartCoroutine(MoveTo(P + fwd * 2.5f, 4f));
        yield return new WaitForSeconds(2.2f);
        lookPoint = P + Quaternion.Euler(0f, 40f, 0f) * fwd * 12f + Vector3.up * 1.5f;
        yield return new WaitForSeconds(2.3f);
        P = basePos;

        Sfx(TrailerAudio.Whoosh, 0.8f);
        yield return new WaitForSeconds(0.9f);
        Hit(0.8f, true);
        yield return Card("SOMETHING IS HUNTING", "", 2f, 0.85f);

        // A Demogorgon claws out of the street ahead... then charges. Open fire.
        lookSpeed = 5f;
        fwd = Flat(camT.forward);
        var d1 = spawner.SpawnAt(PointAt(P, fwd, 9f), DemogorgonType.Normal, true, 1.1f);
        lookPoint = d1.transform.position + Vector3.up * 1.4f;
        GameFx.Play2D(spawnClip, 0.9f);
        Hit(0.6f);
        Music(arpSrc, 0.55f, 1.5f);
        yield return new WaitForSeconds(2.2f);
        yield return FireAt(d1.transform, 8, 0.11f);

        // Jump scare: snap left, a Pouncer leaps - sword slash.
        Vector3 left = Quaternion.Euler(0f, -75f, 0f) * fwd;
        var d2 = spawner.SpawnAt(PointAt(P, left, 5.5f), DemogorgonType.Pouncer, false, 1.3f);
        if (d2.TryGetComponent(out PounceBehaviour pb)) pb.LeapSoon(0.25f);
        GameFx.Play2D(DemogorgonAI.AttackClip, 1f, 0.8f);
        Hit(1f);
        lookSpeed = 14f;
        lookPoint = d2.transform.position + Vector3.up * 1.2f;
        shake = 1f;
        yield return Track(d2.transform, 0.45f);
        Time.timeScale = 0.3f; // slow-motion beat: music drops in pitch
        arpSrc.pitch = 0.5f;
        droneSrc.pitch = 0.7f;
        Sfx(TrailerAudio.Whoosh, 1f, 0.6f);
        yield return Track(d2.transform, 0.25f);
        yield return Slash(d2);
        yield return new WaitForSeconds(0.25f);
        Time.timeScale = 1f;
        arpSrc.pitch = 1f;
        droneSrc.pitch = 1f;
        yield return new WaitForSeconds(0.4f);

        Hit(1f, true);
        yield return Card("FIGHT BACK", "", 1.6f, 0.85f);
        Music(arpSrc, 0.75f, 1f);

        // A pack of Runners down the street - mow them down.
        lookSpeed = 6f;
        fwd = Flat(camT.forward);
        var pack = new GameObject[3];
        for (int i = 0; i < 3; i++)
            pack[i] = spawner.SpawnAt(PointAt(P, Quaternion.Euler(0f, (i - 1) * 18f, 0f) * fwd, 15f + i * 2f), DemogorgonType.Runner, false, 1.2f);
        lookPoint = pack[1].transform.position + Vector3.up;
        yield return new WaitForSeconds(1.1f);
        foreach (var r in pack) yield return FireAt(r != null ? r.transform : null, 5, 0.1f);

        // Look up: bats swirl overhead.
        lookSpeed = 2f;
        lookPoint = P + Flat(camT.forward) * 4f + Vector3.up * 14f;
        yield return new WaitForSeconds(2.6f);

        // Far away, a red beam pierces the sky (teased, not explained).
        Vector3 beamDir = Quaternion.Euler(0f, 110f, 0f) * Flat(camT.forward);
        var beamGate = UpsideDownGate.Create(PointAt(P, beamDir, 40f), spawner.PortalTemplate, 99999f, 0);
        var lbl = beamGate.transform.Find("Gate Label");
        if (lbl != null) lbl.gameObject.SetActive(false);
        lookSpeed = 2.2f;
        lookPoint = beamGate.transform.position + Vector3.up * 12f;
        Sfx(TrailerAudio.Braam, 0.6f, 0.8f);
        yield return new WaitForSeconds(3f);

        // Lightning-lit glimpse of something HUGE. Cut before it is clear.
        lookSpeed = 3f;
        Vector3 bossDir = Quaternion.Euler(0f, -40f, 0f) * Flat(camT.forward);
        var boss = spawner.SpawnAt(PointAt(P, bossDir, 24f), DemogorgonType.Boss, true, 0.3f);
        yield return null;
        var bar = GameObject.Find("Boss Bar");
        if (bar != null) Destroy(bar); // no name spoilers
        lookPoint = boss.transform.position + Vector3.up * boss.transform.lossyScale.y * 60f;
        SetSun(Color.black, 0f);
        Music(arpSrc, 0f, 0.3f); // sudden silence
        Music(droneSrc, 0.25f, 0.3f);
        Sfx(heartbeat, 1f, 1.1f);
        yield return new WaitForSeconds(1.2f);
        GameFx.Play2D(DemogorgonAI.AttackClip, 1f, 0.55f);
        Hit(1.3f, true);
        for (int i = 0; i < 3; i++)
        {
            SetSun(new Color(0.75f, 0.8f, 1f), 2.2f);
            shake = 0.5f;
            yield return new WaitForSeconds(0.08f);
            SetSun(Color.black, 0f);
            yield return new WaitForSeconds(0.25f);
        }
        GameFx.Play2D(GameFx.Thunder, 1f, 0.8f);
        yield return new WaitForSeconds(0.4f);
        SetCard(1f, "", "");

        // ---------------- TITLE (0:47 - 1:00) ----------------
        Sfx(TrailerAudio.Riser(1.2f), 0.9f);
        yield return new WaitForSeconds(1.2f);
        Hit(1.5f, true);
        Music(droneSrc, 0.7f, 0.5f);
        yield return Card("SAVE THE HAWKINS", "DEMO HUNT", 7f, 1f, 1.15f);
        Sfx(TrailerAudio.Whoosh, 0.6f, 1.2f);
        Sfx(TrailerAudio.Ting, 0.5f);
        yield return Card("PLAY NOW", "ON META QUEST", 5.5f, 1f);
        Music(droneSrc, 0f, 1f);
        SetCard(1f, "", "");
        yield return new WaitForSeconds(1f);
        Finished = true;
        Debug.Log("Trailer finished.");
    }

    // ===================================================================
    //  Rig: camera + first-person rifle and crystal sword
    // ===================================================================
    void BuildCamera()
    {
        var go = new GameObject("Trailer Camera");
        DontDestroyOnLoad(go);
        go.tag = CameraTag;
        cam = go.AddComponent<Camera>();
        cam.fieldOfView = 72f;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 800f;
        cam.depth = 100f;
        camT = go.transform;

        BuildGrade();
        BuildGun();
        BuildSword();

        // Cards + letterbox, all parented to the camera.
        var bg = GameFx.GlowPrimitive(PrimitiveType.Quad, Color.black, new Vector3(4f, 2.5f, 1f), camT);
        bg.transform.localPosition = new Vector3(0f, 0f, 0.6f);
        bg.transform.localScale = new Vector3(2f, 1.2f, 1f);
        cardBg = bg.GetComponent<Renderer>();
        cardBg.material = new Material(cardBg.sharedMaterial);
        for (int s = -1; s <= 1; s += 2)
        {
            var bar = GameFx.GlowPrimitive(PrimitiveType.Quad, Color.black, new Vector3(2f, 0.1f, 1f), camT);
            bar.transform.localPosition = new Vector3(0f, s * 0.395f, 0.62f);
        }

        // HDR face colours: stay bright through the ACES grade and bloom into a neon glow.
        cardTitle = CardText("Title", 0.85f, new Color(1f, 0.16f, 0.1f) * 4f, new Color(1f, 0.1f, 0.05f, 0.9f), 0.12f, new Color32(255, 90, 60, 255));
        cardTitle.transform.localPosition = new Vector3(0f, 0.03f, 0.58f);
        cardSub = CardText("Sub", 0.32f, new Color(1f, 0.97f, 0.94f) * 2.2f, new Color(1f, 0.6f, 0.5f, 0.35f), 0.1f, new Color32(20, 0, 0, 255));
        cardSub.transform.localPosition = new Vector3(0f, -0.11f, 0.58f);
        cardSub.characterSpacing = 30f;
        caption = CardText("Caption", 0.22f, new Color(1f, 0.97f, 0.94f) * 2.2f, new Color(1f, 1f, 1f, 0.3f), 0.1f, new Color32(10, 0, 0, 255));
        caption.transform.localPosition = new Vector3(0f, -0.28f, 0.58f);
        caption.characterSpacing = 25f;
        SetCard(0f, "", "");
    }

    // Blockbuster look: ACES, teal shadows / hot red-orange highlights, bloom, grain, vignette.
    void BuildGrade()
    {
        var acd = cam.GetUniversalAdditionalCameraData();
        acd.renderPostProcessing = true;
        acd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        acd.antialiasingQuality = AntialiasingQuality.High;

        var vgo = new GameObject("Trailer Grade");
        DontDestroyOnLoad(vgo);
        var vol = vgo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1000f;
        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = p;

        p.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bloom = p.Add<Bloom>(true);
        bloom.intensity.Override(1.6f);
        bloom.threshold.Override(0.8f);
        bloom.scatter.Override(0.8f);
        bloom.tint.Override(new Color(1f, 0.85f, 0.8f));
        var ca = p.Add<ColorAdjustments>(true);
        grade = ca;
        ca.contrast.Override(25f);
        ca.saturation.Override(-10f);
        ca.postExposure.Override(0.35f);
        var st = p.Add<SplitToning>(true);
        st.shadows.Override(new Color(0.18f, 0.34f, 0.5f));
        st.highlights.Override(new Color(1f, 0.42f, 0.28f));
        st.balance.Override(-10f);
        var vig = p.Add<Vignette>(true);
        vig.intensity.Override(0.4f);
        vig.smoothness.Override(0.55f);
        var grain = p.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Medium2);
        grain.intensity.Override(0.3f);
        chroma = p.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0.15f);
        lens = p.Add<LensDistortion>(true);
        lens.intensity.Override(0f);
        var mb = p.Add<MotionBlur>(true);
        mb.mode.Override(MotionBlurMode.CameraOnly);
        mb.quality.Override(MotionBlurQuality.High);
        mb.intensity.Override(0.35f);
    }

    TextMeshPro CardText(string n, float size, Color hdrFace, Color glow, float outline, Color32 outlineColor)
    {
        var t = GameHUD.CreateText(n, size, Color.white, new Vector2(1.4f, 0.4f), camT);
        if (font != null) t.font = font;
        t.fontStyle = FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.characterSpacing = 8f;
        var m = t.fontMaterial; // per-text instance
        m.SetColor("_FaceColor", hdrFace);
        m.EnableKeyword("GLOW_ON");
        m.SetColor("_GlowColor", glow);
        m.SetFloat("_GlowOuter", 0.35f);
        m.SetFloat("_GlowPower", 0.6f);
        t.outlineWidth = outline;
        t.outlineColor = outlineColor;
        return t;
    }

    void BuildGun()
    {
        var prefab = Load<GameObject>("Assets/BigRookGames/_AssetPacks/Stylized Weapon Pack/M4 Scoped Assault Rifle/Prefabs/Assault_Rifle_M4.prefab");
        var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>(true) : null;
        if (mf == null) return;
        var vrgun = FindFirstObjectByType<VRGun>(FindObjectsInactive.Include);
        if (vrgun != null && typeof(VRGun).GetField("shotSound", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(vrgun) is AudioSource src)
            gunshot = src.clip;

        var g = new GameObject("FP Rifle");
        g.transform.SetParent(camT, false);
        g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var mr = g.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Mesh barrel runs along -X with +Z up; point it along the camera's forward.
        g.transform.localRotation = new Quaternion(-0.5f, 0.5f, 0.5f, 0.5f);
        g.transform.localScale = Vector3.one * 0.25f;
        Bounds b = LocalBounds(g);
        g.transform.localPosition += new Vector3(0.17f, -0.17f, 0.12f) - new Vector3(b.center.x, b.max.y, b.min.z);
        b = LocalBounds(g);
        muzzleLocal = new Vector3(b.center.x, b.center.y + b.extents.y * 0.3f, b.max.z);
        gunModel = g.transform;
    }

    void BuildSword()
    {
        var model = Load<GameObject>("Assets/Resources/shattered-crystal-sword/source/Crystal Sword.fbx");
        var tex = Load<Texture2D>("Assets/Resources/shattered-crystal-sword/textures/T_crystal_Sword.png");
        if (model == null) return;
        swordPivot = new GameObject("FP Sword Pivot").transform;
        swordPivot.SetParent(camT, false);
        swordPivot.localPosition = new Vector3(-0.27f, -0.36f, 0.34f);
        swordRest = Quaternion.Euler(-35f, 15f, 20f);
        swordPivot.localRotation = swordRest;

        var m = Instantiate(model, swordPivot);
        foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.SetTexture("_EmissionMap", tex); }
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1f) * 1.6f);
        mat.SetFloat("_Smoothness", 0.85f);
        foreach (var r in m.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }

        m.transform.localRotation = Quaternion.identity;
        Bounds b = BoundsIn(m, swordPivot);
        Vector3 s = b.size;
        m.transform.localRotation = s.x >= s.y && s.x >= s.z ? Quaternion.FromToRotation(Vector3.right, Vector3.up)
            : s.z >= s.y ? Quaternion.FromToRotation(Vector3.forward, Vector3.up) : Quaternion.identity;
        b = BoundsIn(m, swordPivot);
        m.transform.localScale *= 0.7f / Mathf.Max(0.001f, b.size.y);
        b = BoundsIn(m, swordPivot);
        m.transform.localPosition -= new Vector3(b.center.x, b.min.y + 0.1f, b.center.z); // grip at pivot, blade up
    }

    Bounds LocalBounds(GameObject go) => BoundsIn(go, camT);

    static Bounds BoundsIn(GameObject go, Transform space)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = new Bounds(space.InverseTransformPoint(rs.Length > 0 ? rs[0].bounds.center : go.transform.position), Vector3.zero);
        foreach (var r in rs)
        {
            Bounds wb = r.bounds;
            for (int i = 0; i < 8; i++)
                b.Encapsulate(space.InverseTransformPoint(wb.center + Vector3.Scale(wb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        }
        return b;
    }

    // Head motion: smooth look, walk bob, recoil, shake.
    void LateUpdate()
    {
        if (camT == null) return;
        float dt = Time.deltaTime;
        if (walking) bobTime += dt * 9f;
        Vector3 bob = walking ? new Vector3(Mathf.Sin(bobTime * 0.5f) * 0.03f, Mathf.Abs(Mathf.Sin(bobTime)) * 0.045f, 0f) : Vector3.zero;
        Vector3 shk = Random.insideUnitSphere * shake * 0.03f;
        camT.position = basePos + camT.rotation * bob + shk;

        Vector3 dir = lookPoint - camT.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion want = Quaternion.LookRotation(dir);
            float idle = Mathf.Sin(Time.time * 0.7f) * 0.6f;
            camT.rotation = Quaternion.Slerp(camT.rotation, want, 1f - Mathf.Exp(-lookSpeed * dt)) * Quaternion.Euler(-recoil + idle * 0.3f, idle * 0.2f, 0f);
        }
        recoil = Mathf.Lerp(recoil, 0f, 14f * dt);
        shake = Mathf.Lerp(shake, 0f, 3f * dt);
        if (gunModel != null) gunModel.localRotation = new Quaternion(-0.5f, 0.5f, 0.5f, 0.5f) * Quaternion.Euler(0f, 0f, recoil * 2.5f);
    }

    // ===================================================================
    //  Actions
    // ===================================================================
    IEnumerator MoveTo(Vector3 to, float t)
    {
        walking = true;
        Vector3 from = basePos;
        for (float e = 0f; e < t; e += Time.deltaTime)
        {
            float k = e / t;
            basePos = Vector3.Lerp(from, to, k * k * (3f - 2f * k));
            yield return null;
        }
        basePos = to;
        walking = false;
    }

    IEnumerator Track(Transform target, float t)
    {
        for (float e = 0f; e < t && target != null; e += Time.deltaTime)
        {
            lookPoint = target.position + Vector3.up * 1.2f;
            yield return null;
        }
    }

    IEnumerator FireAt(Transform target, int shots, float interval)
    {
        for (int i = 0; i < shots && target != null; i++)
        {
            var h = target.GetComponent<DemogorgonHealth>();
            if (h == null || h.IsDead) break;
            Vector3 aim = target.position + Vector3.up * target.lossyScale.y * 50f;
            lookPoint = aim;
            yield return new WaitForSeconds(interval * 0.5f);
            Shoot(aim);
            yield return new WaitForSeconds(interval * 0.5f);
        }
        // Make sure the shot lands for the cut.
        if (target != null && target.TryGetComponent(out DemogorgonHealth hh) && !hh.IsDead) hh.TakeDamage(9999f);
        yield return new WaitForSeconds(0.35f);
    }

    void Shoot(Vector3 aim)
    {
        Vector3 muzzle = camT.TransformPoint(muzzleLocal);
        recoil += 2.2f;
        shake = Mathf.Max(shake, 0.15f);
        if (gunshot != null) GameFx.Play2D(gunshot, 0.9f, Random.Range(0.95f, 1.05f));
        var flash = GameFx.GlowPrimitive(PrimitiveType.Sphere, new Color(1f, 0.85f, 0.4f), Vector3.one * 0.07f);
        flash.transform.position = muzzle;
        Destroy(flash, 0.04f);
        var l = GameFx.CreateLight(flash.transform, Vector3.zero, new Color(1f, 0.75f, 0.35f), 8f, 5f);
        if (bulletPrefab == null) return;
        var bullet = Instantiate(bulletPrefab, muzzle + (aim - muzzle).normalized * 0.4f, Quaternion.LookRotation(aim - muzzle));
        var bc = bullet.GetComponent<Collider>();
        if (bc != null && Camera.main != null)
            foreach (var c in Camera.main.transform.root.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(bc, c);
        if (bullet.TryGetComponent(out Rigidbody rb)) rb.linearVelocity = (aim - muzzle).normalized * 70f;
    }

    IEnumerator Slash(GameObject target)
    {
        if (swordPivot != null)
        {
            Quaternion a = Quaternion.Euler(-70f, -50f, 60f), b = Quaternion.Euler(20f, 60f, -30f);
            for (float e = 0f; e < 0.12f; e += Time.deltaTime) { swordPivot.localRotation = Quaternion.Slerp(swordRest, a, e / 0.12f); yield return null; }
            for (float e = 0f; e < 0.16f; e += Time.deltaTime)
            {
                swordPivot.localRotation = Quaternion.Slerp(a, b, e / 0.16f);
                if (e > 0.06f && target != null && target.TryGetComponent(out DemogorgonHealth h) && !h.IsDead)
                {
                    h.TakeDamage(9999f);
                    GameFx.Burst(target.transform.position + Vector3.up * 1.2f, 14, 4f, 0.5f, Color.cyan, Color.white, Color.magenta);
                    GameFx.Play2D(GameFx.Boom, 0.6f, 1.8f);
                    shake = 0.8f;
                }
                yield return null;
            }
            for (float e = 0f; e < 0.35f; e += Time.deltaTime) { swordPivot.localRotation = Quaternion.Slerp(b, swordRest, e / 0.35f); yield return null; }
        }
        else if (target != null && target.TryGetComponent(out DemogorgonHealth h2)) h2.TakeDamage(9999f);
    }

    // ===================================================================
    //  Cards, fades, lightning
    // ===================================================================
    IEnumerator Card(string title, string sub, float t, float bgAlpha, float titleScale = 1f)
    {
        cardTitle.transform.localScale = Vector3.one * titleScale;
        const float fade = 0.4f;
        for (float e = 0f; e < t; e += Time.deltaTime)
        {
            float a = Mathf.Clamp01(e / fade) * Mathf.Clamp01((t - e) / fade);
            // Title slowly grows, letters track in; main title flickers on like a dying neon.
            cardTitle.transform.localScale = Vector3.one * titleScale * (1f + e / t * 0.06f);
            cardTitle.characterSpacing = 6f + 18f * Mathf.Exp(-e * 3f);
            if (titleScale > 1f && e < 0.7f && Random.value < 0.35f) a *= 0.15f;
            SetCard(Mathf.Max(a * bgAlpha, cardBg.enabled && bgAlpha >= 1f ? 1f : 0f), title, sub, a);
            yield return null;
        }
        SetCard(bgAlpha >= 1f ? 1f : 0f, "", "");
    }

    void SetCard(float bgAlpha, string title, string sub, float textAlpha = 1f)
    {
        cardBg.material.color = new Color(0f, 0f, 0f, bgAlpha);
        cardBg.enabled = bgAlpha > 0.001f;
        bool showHands = armed && bgAlpha < 0.6f; // rifle/sword sit in front of the card plane
        if (gunModel != null) gunModel.gameObject.SetActive(showHands);
        if (swordPivot != null) swordPivot.gameObject.SetActive(showHands);
        cardTitle.text = title;
        cardSub.text = sub;
        cardTitle.alpha = textAlpha;
        cardSub.alpha = textAlpha;
    }

    void Caption(string text, float t) => StartCoroutine(CaptionRoutine(text, t));

    IEnumerator CaptionRoutine(string text, float t)
    {
        caption.text = text;
        for (float e = 0f; e < t; e += Time.deltaTime)
        {
            caption.alpha = Mathf.Clamp01(e / 0.6f) * Mathf.Clamp01((t - e) / 0.6f);
            yield return null;
        }
        caption.text = "";
    }

    IEnumerator FadeFromBlack(float t)
    {
        for (float e = 0f; e < t; e += Time.deltaTime) { SetCard(1f - e / t, "", ""); yield return null; }
        SetCard(0f, "", "");
    }

    IEnumerator Flash(Color c, float t)
    {
        cardBg.enabled = true;
        for (float e = 0f; e < t; e += Time.deltaTime)
        {
            cardBg.material.color = new Color(c.r, c.g, c.b, e / t);
            yield return null;
        }
        cardBg.material.color = Color.black;
    }

    IEnumerator Ramp(System.Action<float> set, float t)
    {
        for (float e = 0f; e < t; e += Time.deltaTime) { set(e / t); yield return null; }
        set(1f);
    }

    IEnumerator Lightning(float delay)
    {
        yield return new WaitForSeconds(delay);
        GameFx.Play2D(GameFx.Thunder, 1f);
        for (int i = 0; i < 3; i++)
        {
            SetSun(new Color(0.75f, 0.8f, 1f), 2f);
            yield return new WaitForSeconds(0.07f);
            SetSun(sunColor, sunIntensity);
            yield return new WaitForSeconds(0.12f);
        }
    }

    void SetSun(Color c, float i)
    {
        if (sun == null) return;
        sun.color = c;
        sun.intensity = i;
    }

    // ===================================================================
    //  Scene setup + helpers
    // ===================================================================
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Hawkin City") return;
        // Cinematic mode: no task board/radio/waves, no fade screen.
        var director = FindFirstObjectByType<DemoHuntDirector>();
        if (director != null) Destroy(director);
        var fade = FindFirstObjectByType<SceneFadeIn>();
        if (fade != null)
        {
            fade.enabled = false;
            if (Field<CanvasGroup>(fade, "fadeCanvas") is CanvasGroup cg) cg.alpha = 0f;
            Field<TextMeshProUGUI>(fade, "enteringText")?.gameObject.SetActive(false);
            Field<AudioSource>(fade, "thunderSound")?.Play();
        }
        StartCoroutine(CitySetup());
    }

    void Update()
    {
        if (!cityReady) return;
        // Enemy health bars, and the player-rig panels (HellFire Points, health, timer...),
        // which stay with the hidden VR head instead of the trailer camera. Runtime only:
        // nothing is saved, so they are back to normal outside the trailer.
        Transform rig = Camera.main != null ? Camera.main.transform.root : null;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.name.StartsWith("Health Canvas") || (rig != null && c.transform.IsChildOf(rig))) c.enabled = false;
        foreach (var n in new[] { "scoreText", "scoreText2", "scoreText3" })
            if (Field<TextMeshProUGUI>(UIManager.Instance, n) is TextMeshProUGUI t)
                foreach (var c in t.GetComponentsInParent<Canvas>(true)) c.enabled = false;
    }

    IEnumerator CitySetup()
    {
        yield return null;
        yield return null;
        spawner = FindFirstObjectByType<EnemySpawner>();
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; sunColor = l.color; sunIntensity = l.intensity; break; }
        cityReady = spawner != null;
        if (!cityReady) Debug.LogError("Trailer: no EnemySpawner in Hawkin City.");
    }

    Vector3 Eye()
    {
        var main = Camera.main;
        Vector3 p = main != null ? main.transform.position : Vector3.up * 1.6f;
        if (NavMesh.SamplePosition(p, out NavMeshHit h, 4f, NavMesh.AllAreas) && p.y - h.position.y < 1.3f)
            p.y = h.position.y + 1.65f;
        return p;
    }

    static Vector3 GateCenter(Transform gate)
    {
        var rs = gate.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return gate.position;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }

    Vector3 PointAt(Vector3 from, Vector3 dir, float dist)
    {
        Vector3 c = from + dir.normalized * dist;
        for (int i = 0; i < 6; i++)
        {
            if (NavMesh.SamplePosition(c, out NavMeshHit h, 3f + i, NavMesh.AllAreas)) return h.position;
            c = from + Quaternion.Euler(0f, (i % 2 == 0 ? 1 : -1) * 20f * (i + 1), 0f) * dir.normalized * dist;
        }
        return new Vector3(c.x, from.y - 1.6f, c.z);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.001f ? v.normalized : Vector3.forward; }

    static T Field<T>(object o, string name) where T : class =>
        o == null ? null : o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)?.GetValue(o) as T;

    static void Invoke(object o, string method) =>
        o?.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)?.Invoke(o, null);
}
