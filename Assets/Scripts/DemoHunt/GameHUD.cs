using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// World-space VR HUD: big wave banners and walkie-talkie radio subtitles that
/// float in front of the player and gently follow their head.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    private Transform cam;
    private TextMeshPro banner;
    private TextMeshPro radio;
    private Coroutine bannerRoutine;
    private readonly Queue<string> radioQueue = new Queue<string>();
    private bool radioBusy;

    public static GameHUD Create()
    {
        if (Instance != null) return Instance;
        GameObject go = new GameObject("Game HUD");
        return go.AddComponent<GameHUD>();
    }

    void Awake()
    {
        Instance = this;
        cam = Camera.main != null ? Camera.main.transform : null;

        banner = CreateText("Banner", 3f, new Color(1f, 0.15f, 0.1f), new Vector2(4f, 1.2f));
        radio = CreateText("Radio", 0.75f, new Color(0.75f, 1f, 0.75f), new Vector2(1.5f, 0.6f));
        banner.gameObject.SetActive(false);
        radio.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static TextMeshPro CreateText(string name, float size, Color color, Vector2 box, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.rectTransform.sizeDelta = box;
        t.fontStyle = FontStyles.Bold;
        t.outlineWidth = 0.2f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        return t;
    }

    // ---------- Banner ----------

    public static void ShowBanner(string title, string subtitle = null, float duration = 3.5f)
    {
        if (Instance == null) Create();
        Instance.ShowBannerInternal(title, subtitle, duration);
    }

    void ShowBannerInternal(string title, string subtitle, float duration)
    {
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        banner.text = subtitle == null ? title : $"{title}\n<size=50%>{subtitle}</size>";
        bannerRoutine = StartCoroutine(BannerRoutine(duration));
    }

    IEnumerator BannerRoutine(float duration)
    {
        banner.gameObject.SetActive(true);
        PlaceInFront(banner.transform, 3f, 0.35f, true);
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((duration - t) / 0.6f);
            banner.alpha = a;
            FollowHead(banner.transform, 3f, 0.35f);
            yield return null;
        }
        banner.gameObject.SetActive(false);
        bannerRoutine = null;
    }

    // ---------- Radio ----------

    /// <summary>Walkie-talkie message: static burst + subtitle. Messages queue up.</summary>
    public static void Radio(string message)
    {
        if (Instance == null) Create();
        Instance.radioQueue.Enqueue(message);
        if (!Instance.radioBusy)
            Instance.StartCoroutine(Instance.RadioRoutine());
    }

    IEnumerator RadioRoutine()
    {
        radioBusy = true;
        while (radioQueue.Count > 0)
        {
            string msg = radioQueue.Dequeue();
            GameFx.Play2D(GameFx.RadioStatic, 0.35f);
            radio.text = "<color=#FFD24D><size=70%>HAWKINS LAB  ))</size></color>\n" + msg;
            radio.gameObject.SetActive(true);
            PlaceInFront(radio.transform, 1.8f, -0.12f, true);

            float duration = Mathf.Clamp(1.8f + msg.Length * 0.05f, 2.5f, 6f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                radio.alpha = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((duration - t) / 0.4f);
                FollowHead(radio.transform, 1.8f, -0.12f);
                yield return null;
            }
            GameFx.Play2D(GameFx.Beep, 0.25f);
            radio.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.3f);
        }
        radioBusy = false;
    }

    // ---------- Task board ----------

    /// <summary>
    /// Mission panel with a dark background. If waitForTrigger, stays until the player pulls
    /// a trigger (or maxSeconds). Yield on the returned coroutine to wait for it.
    /// </summary>
    public static Coroutine ShowTaskBoard(string title, string[] tasks, bool waitForTrigger, float maxSeconds)
    {
        if (Instance == null) Create();
        return Instance.StartCoroutine(Instance.TaskBoardRoutine(title, tasks, waitForTrigger, maxSeconds));
    }

    // Style shared with the game's menu panels (Guide / Description). Set by DemoHuntDirector.
    public static Sprite PanelSprite, ButtonSprite;
    public static TMP_FontAsset PanelFont;

    IEnumerator TaskBoardRoutine(string title, string[] tasks, bool waitForTrigger, float maxSeconds)
    {
        ResolvePanelStyle();
        // World-space canvas laid out like the Guide panel (3850 x 2800 background).
        var root = new GameObject("Task Board", typeof(RectTransform));
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(3850f, 2800f);
        rt.localScale = Vector3.one * 0.00062f; // ~2.4 m wide at 2.6 m distance

        var bg = UI<UnityEngine.UI.Image>("BG Img", rt, Vector2.zero, new Vector2(3850f, 2800f));
        if (PanelSprite != null) bg.sprite = PanelSprite;
        else bg.color = new Color(0.05f, 0.02f, 0.02f, 0.92f);

        // Title: one line, inside the frame.
        var head = UI<TextMeshProUGUI>("Title", rt, new Vector2(0f, 820f), new Vector2(2900f, 300f));
        StyleText(head, 240f, new Color(1f, 0.19215687f, 0.19215687f), FontStyles.Bold, TextAlignmentOptions.Center);
        SingleLine(head, 120f, 240f);
        head.text = title;

        // One row per task: fixed checkbox column + single-line text column, so every
        // line starts at the same x and nothing wraps. Format: "1|done", "0|open", "B|bonus".
        Color bodyColor = new Color(0.9433962f, 0.91224635f, 0.91224635f);
        // Block of width boxW+gap+textW centred on the panel.
        const float boxW = 200f, gap = 40f, textW = 2400f, left = -(boxW + gap + textW) * 0.5f, rowStep = 260f;
        float y = 420f;
        foreach (string line in tasks)
        {
            string kind = line.Length > 2 && line[1] == '|' ? line.Substring(0, 1) : "0";
            string text = line.Length > 2 && line[1] == '|' ? line.Substring(2) : line;
            if (kind == "B")
            {
                var bonus = UI<TextMeshProUGUI>("Bonus", rt, new Vector2(0f, y - 60f), new Vector2(3000f, 200f));
                StyleText(bonus, 85f, new Color(0.75f, 0.75f, 0.75f), FontStyles.Normal, TextAlignmentOptions.Center);
                SingleLine(bonus, 55f, 85f);
                bonus.text = text;
                y -= rowStep;
                continue;
            }
            var box = UI<TextMeshProUGUI>("Box", rt, new Vector2(left + boxW * 0.5f, y), new Vector2(boxW, 200f));
            StyleText(box, 115f, kind == "1" ? new Color(0.49f, 1f, 0.49f) : new Color(1f, 0.19215687f, 0.19215687f), FontStyles.Bold, TextAlignmentOptions.Center);
            box.text = kind == "1" ? "[X]" : "[  ]";
            var row = UI<TextMeshProUGUI>("Task", rt, new Vector2(left + boxW + gap + textW * 0.5f, y), new Vector2(textW, 200f));
            StyleText(row, 115f, bodyColor, FontStyles.Normal, TextAlignmentOptions.Left);
            SingleLine(row, 70f, 115f);
            row.text = text;
            y -= rowStep;
        }

        if (waitForTrigger)
        {
            // Centred prompt along the bottom of the frame.
            var hint = UI<TextMeshProUGUI>("Hint", rt, new Vector2(0f, -900f), new Vector2(2800f, 220f));
            StyleText(hint, 110f, new Color(1f, 0.19215687f, 0.19215687f), FontStyles.Bold, TextAlignmentOptions.Center);
            SingleLine(hint, 70f, 110f);
            hint.text = "PULL TRIGGER TO BEGIN";
        }

        PlaceInFront(rt, 2.6f, 0.1f, true);
        GameFx.Play2D(GameFx.Beep, 0.4f, 0.8f);

        float t = 0f;
        bool released = !TriggerHeld();
        while (t < maxSeconds)
        {
            t += Time.unscaledDeltaTime;
            FollowHead(rt, 2.6f, 0.1f);
            if (waitForTrigger && t > 1f)
            {
                bool held = TriggerHeld();
                if (released && held) break;
                if (!held) released = true;
            }
            yield return null;
        }
        GameFx.Play2D(GameFx.Beep, 0.4f, 1.3f);
        Destroy(root);
    }

    // Falls back to the sprites/font the scene's own menu panels already use,
    // so the task board matches even if the Director fields are not wired.
    static void ResolvePanelStyle()
    {
        if (PanelSprite == null || ButtonSprite == null)
            foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (PanelSprite == null && sp.name == "BackgroundImg") PanelSprite = sp;
                if (ButtonSprite == null && sp.name == "NextBtn_UI") ButtonSprite = sp;
            }
        if (PanelFont == null)
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (f.name.StartsWith("Benguiat")) { PanelFont = f; break; }
        if (PanelSprite == null) Debug.LogWarning("Task board: BackgroundImg sprite not found, using plain panel.");
    }

    static void SingleLine(TextMeshProUGUI t, float min, float max)
    {
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMin = min;
        t.fontSizeMax = max;
        t.overflowMode = TextOverflowModes.Ellipsis;
    }

    static T UI<T>(string name, RectTransform parent, Vector2 pos, Vector2 size) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return go.AddComponent<T>();
    }

    static void StyleText(TextMeshProUGUI t, float size, Color color, FontStyles style, TextAlignmentOptions align)
    {
        if (PanelFont != null) t.font = PanelFont;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
    }

    static bool TriggerHeld()
    {
        foreach (var node in new[] { UnityEngine.XR.XRNode.LeftHand, UnityEngine.XR.XRNode.RightHand })
        {
            var d = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool p) && p) return true;
        }
        return false;
    }

    // ---------- Placement ----------

    void PlaceInFront(Transform t, float distance, float height, bool snap)
    {
        if (cam == null) cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;
        Vector3 fwd = cam.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 target = cam.position + fwd * distance + Vector3.up * height;
        t.position = snap ? target : Vector3.Lerp(t.position, target, 3f * Time.unscaledDeltaTime);
        GameFx.Billboard(t, cam);
    }

    void FollowHead(Transform t, float distance, float height) => PlaceInFront(t, distance, height, false);
}
