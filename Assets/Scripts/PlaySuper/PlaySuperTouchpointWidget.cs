using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Shared renderer for this game's touchpoints.
//
// Both live touchpoints are the `home-entry-widget` pattern: one leaf node carrying a single
// baked image plus a CTA, and the whole widget is the tap target. The live shape report lists
// cta.text, cta.backgroundImage, background, overlay, title, subtitle, badge, dynamicConfig and
// popup as ABSENT for both, so there is deliberately no code here for any of them - no null
// guard ladders for data these touchpoints never carry.
//
// Mounting (parenting into the screen's own canvas, sibling ordering, visibility) is
// PlaySuperWidgetMount's job. This class owns fetch, art, placement and lifetime.
public abstract class PlaySuperTouchpointWidget : MonoBehaviour
{
    // The touchpoint's lowercase-kebab name; also the utm_content the store sees.
    protected abstract string TouchpointName { get; }

    // This touchpoint's OWN approved placement: normalized 0-1, TOP-LEFT origin.
    protected abstract Rect Bbox { get; }

    RectTransform self;
    RawImage art;
    Texture2D artTexture;
    bool building;
    int lastScreenWidth;
    int lastScreenHeight;
    bool audioPausedByStore;

    void Awake()
    {
        self = GetComponent<RectTransform>();
    }

    // OnEnable, not Start: a screen shown a second time never re-runs Start.
    void OnEnable()
    {
        // These two are STATIC events. Subscribing through .Instance is CS0176.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;

        ApplyPlacement();

        if (art == null && !building)
        {
            _ = Build();
        }
    }

    void OnDisable()
    {
        PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
        RestoreAudio();
        Teardown();
    }

    void OnDestroy()
    {
        RestoreAudio();
        Teardown();
    }

    async Task Build()
    {
        // OnEnable can fire again before a download finishes, hence the in-flight flag.
        building = true;
        try
        {
            TouchpointResponse tp = await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperBootstrap.CoinId);

            // Fail silent-and-absent: a touchpoint that will not load must never break the
            // screen it sits on.
            if (this == null || !isActiveAndEnabled) return;
            if (tp == null || tp.nodes == null || tp.nodes.Length == 0) return;

            TouchpointNode node = tp.nodes[0];
            if (node == null || node.images == null || node.images.Length == 0) return;

            string imageUrl = node.images[0];
            if (string.IsNullOrEmpty(imageUrl)) return;

            Texture2D texture = await Download(imageUrl);

            if (this == null || !isActiveAndEnabled)
            {
                if (texture != null) Destroy(texture);
                return;
            }
            if (texture == null) return;

            artTexture = texture;
            BuildArt(node, texture);
        }
        finally
        {
            building = false;
        }
    }

    void BuildArt(TouchpointNode node, Texture2D texture)
    {
        GameObject go = new GameObject("Art", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(self, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        art = go.AddComponent<RawImage>();
        art.texture = texture;
        art.raycastTarget = true;

        // The fitter refits to its PARENT and ignores sub-region anchors. That is exactly what
        // is wanted here, because the parent IS the approved bbox - the art keeps its own
        // aspect and letterboxes inside the approved box rather than stretching to it.
        AspectRatioFitter fitter = go.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = texture.height > 0 ? (float)texture.width / texture.height : 1f;

        string action = node.cta != null ? node.cta.action : null;

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = art;
        button.onClick.AddListener(delegate { OpenStore(action); });
    }

    static void OpenStore(string action)
    {
        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null) return;

        // In-app WebView, never Application.OpenURL. The action is passed through UNCHANGED -
        // utm_content is already appended server-side.
        if (string.IsNullOrEmpty(action)) sdk.OpenStore();
        else sdk.OpenStore(action);
    }

    // The bbox came from a screenshot, which knows nothing about notches, punch-holes or the
    // home indicator, so it is clamped into the safe area rather than trusted outright. The
    // approved size is kept and the box is SHIFTED; it only shrinks if it cannot fit at all.
    void ApplyPlacement()
    {
        if (self == null) return;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float safeX = 0f;
        float safeY = 0f;
        float safeW = 1f;
        float safeH = 1f;

        Rect safe = Screen.safeArea;
        if (Screen.width > 0 && Screen.height > 0 && safe.width > 0f && safe.height > 0f)
        {
            safeX = safe.x / Screen.width;
            safeY = safe.y / Screen.height;
            safeW = safe.width / Screen.width;
            safeH = safe.height / Screen.height;
        }

        float width = Mathf.Min(Bbox.width, safeW);
        float height = Mathf.Min(Bbox.height, safeH);

        // Top-left origin -> bottom-left origin: y must be flipped. Getting this wrong mirrors
        // the widget vertically, which looks plausible mid-screen and obviously wrong near an edge.
        float xMin = Mathf.Clamp(Bbox.x, safeX, safeX + safeW - width);
        float yMin = Mathf.Clamp(1f - Bbox.y - Bbox.height, safeY, safeY + safeH - height);

        self.anchorMin = new Vector2(xMin, yMin);
        self.anchorMax = new Vector2(xMin + width, yMin + height);
        self.offsetMin = Vector2.zero;
        self.offsetMax = Vector2.zero;
        self.pivot = new Vector2(0.5f, 0.5f);
        self.localScale = Vector3.one;
    }

    // Re-clamp on resolution / orientation change: a top notch moves to the side in landscape.
    // The screen-size check also stops this recursing, since ApplyPlacement resizes this rect.
    void OnRectTransformDimensionsChange()
    {
        if (self == null) return;
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) return;
        ApplyPlacement();
    }

    void HandleStoreOpened()
    {
        if (!AudioListener.pause)
        {
            AudioListener.pause = true;
            audioPausedByStore = true;
        }
    }

    void HandleStoreClosed()
    {
        RestoreAudio();
    }

    void RestoreAudio()
    {
        if (!audioPausedByStore) return;
        AudioListener.pause = false;
        audioPausedByStore = false;
    }

    void Teardown()
    {
        if (art != null)
        {
            Destroy(art.gameObject);
            art = null;
        }

        // UnityWebRequestTexture textures are not garbage collected for you.
        if (artTexture != null)
        {
            Destroy(artTexture);
            artTexture = null;
        }
    }

    static async Task<Texture2D> Download(string url)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            return req.result == UnityWebRequest.Result.Success
                ? DownloadHandlerTexture.GetContent(req)
                : null;
        }
    }
}
