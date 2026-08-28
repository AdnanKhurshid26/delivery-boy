using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Renders the "home-2" touchpoint on the TASK COMPLETED panel - the gameCompletedSceen object
/// that GamePlayManager activates when a level is finished. That panel lives inside the gameplay
/// scene rather than being a scene of its own, so this is mounted by GamePlayManager via
/// MountOn() instead of being gated on a scene name.
///
/// Pattern: home-entry-widget - one leaf ASSET node, a single baked image plus a CTA, with no
/// cta.text or cta.backgroundImage to composite. The whole widget is the tap target.
///
/// Built entirely at runtime. No .unity or .prefab file is touched.
/// </summary>
public class PlaySuperTouchpoint_Home2 : MonoBehaviour
{
    // Fetch key, and the utm_content the store sees.
    private const string TouchpointName = "home-2";

    // Accepted placement: normalized 0-1 with a TOP-LEFT origin.
    private static readonly Rect Bbox = new Rect(0.6508143f, 0.6269504f, 0.1218241f, 0.0609929f);

    private GameObject overlay;

    /// <summary>
    /// Attaches the renderer to the level-complete panel. Safe to call more than once:
    /// gameCompleted() has two callers, so a duplicate mount is expected rather than exceptional.
    /// </summary>
    public static void MountOn(GameObject panel)
    {
        if (panel == null || panel.GetComponent<PlaySuperTouchpoint_Home2>() != null)
        {
            return;
        }

        panel.AddComponent<PlaySuperTouchpoint_Home2>();
    }

    private async void Start()
    {
        PlaySuperStoreEntry.InstallStoreAudio();

        // Every fetch can come back null - the SDK logs and returns null rather than throwing.
        TouchpointResponse touchpoint = await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperBootstrap.CoinId);
        if (this == null)
        {
            return;
        }

        if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
        {
            return; // fail silent-and-absent: the level-complete screen must still work
        }

        TouchpointNode node = touchpoint.nodes[0];
        if (node == null || node.images == null || node.images.Length == 0 || string.IsNullOrEmpty(node.images[0]))
        {
            return;
        }

        Texture2D art = await Download(node.images[0]);
        if (this == null || art == null)
        {
            return;
        }

        Build(art, node.cta != null ? node.cta.action : null);

        // The panel may have been hidden while the art was downloading.
        if (overlay != null)
        {
            overlay.SetActive(gameObject.activeInHierarchy);
        }
    }

    private void Build(Texture2D art, string action)
    {
        // A runtime canvas needs an EventSystem in the scene or taps silently do nothing.
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        overlay = new GameObject("PlaySuperTouchpointCanvas_Home2", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        // Placement container. The bbox has a top-left origin; uGUI anchors run bottom-left.
        GameObject widget = new GameObject("Widget", typeof(RectTransform), typeof(Button));
        widget.transform.SetParent(overlay.transform, false);

        RectTransform widgetRect = widget.GetComponent<RectTransform>();
        widgetRect.anchorMin = new Vector2(Bbox.x, 1f - Bbox.y - Bbox.height);
        widgetRect.anchorMax = new Vector2(Bbox.x + Bbox.width, 1f - Bbox.y);
        widgetRect.offsetMin = Vector2.zero;
        widgetRect.offsetMax = Vector2.zero;

        // The art gets its OWN child rect. AspectRatioFitter.FitInParent refits to the PARENT,
        // so putting the fitter on the anchored container above would throw the placement away.
        GameObject artObject = new GameObject("Art", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        artObject.transform.SetParent(widget.transform, false);

        RectTransform artRect = artObject.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        RawImage raw = artObject.GetComponent<RawImage>();
        raw.texture = art;
        raw.raycastTarget = true;

        AspectRatioFitter fitter = artObject.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = art.height == 0 ? 1f : (float)art.width / art.height;

        Button button = widget.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = raw;
        button.onClick.AddListener(delegate { OpenStore(action); });
    }

    private void OpenStore(string action)
    {
        if (PlaySuperUnitySDK.Instance == null)
        {
            Debug.LogWarning("[PlaySuper] SDK instance is null - cannot open the store.");
            return;
        }

        // Pass the action through UNCHANGED: utm_content is already appended server-side.
        // The in-app WebView, never Application.OpenURL.
        PlaySuperUnitySDK.Instance.OpenStore(action);
    }

    // The widget must never outlive the panel it belongs to.
    private void OnEnable()
    {
        if (overlay != null)
        {
            overlay.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (overlay != null)
        {
            overlay.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (overlay != null)
        {
            Destroy(overlay);
            overlay = null;
        }
    }

    private static async Task<Texture2D> Download(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[PlaySuper] Touchpoint art failed to download: " + request.error);
                return null;
            }

            return DownloadHandlerTexture.GetContent(request);
        }
    }
}
