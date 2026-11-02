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
        radio = CreateText("Radio", 0.9f, new Color(0.75f, 1f, 0.75f), new Vector2(2.2f, 0.6f));
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
            PlaceInFront(radio.transform, 1.6f, -0.45f, true);

            float duration = Mathf.Clamp(1.8f + msg.Length * 0.05f, 2.5f, 6f);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                radio.alpha = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((duration - t) / 0.4f);
                FollowHead(radio.transform, 1.6f, -0.45f);
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

        var head = UI<TextMeshProUGUI>("Title", rt, new Vector2(0f, 950f), new Vector2(2650f, 310f));
        StyleText(head, 300f, new Color(1f, 0.19215687f, 0.19215687f), FontStyles.Bold, TextAlignmentOptions.Center);
        head.text = title;

        var body = UI<TextMeshProUGUI>("Tasks", rt, new Vector2(0f, -80f), new Vector2(3150f, 1730f));
        StyleText(body, 100f, new Color(0.9433962f, 0.91224635f, 0.91224635f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
        body.lineSpacing = 40f;
        body.text = string.Join("\n", tasks);

        if (waitForTrigger)
        {
            var btn = UI<UnityEngine.UI.Image>("Next Btn", rt, new Vector2(1500f, -1120f), new Vector2(200f, 200f));
            if (ButtonSprite != null) btn.sprite = ButtonSprite;
            var hint = UI<TextMeshProUGUI>("Hint", rt, new Vector2(700f, -1120f), new Vector2(1300f, 200f));
            StyleText(hint, 90f, new Color(1f, 0.19215687f, 0.19215687f), FontStyles.Bold, TextAlignmentOptions.Right);
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
