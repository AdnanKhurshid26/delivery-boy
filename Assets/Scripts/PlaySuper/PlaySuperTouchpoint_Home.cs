using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders the "home" touchpoint on the main menu screen (Assets/Scenes/menu.unity).
///
/// Pattern: home-entry-widget - one leaf ASSET node carrying a single baked image plus a CTA.
/// The node has no cta.text and no cta.backgroundImage, so nothing is composited on top of the
/// art: the whole widget is the tap target.
///
/// Built entirely at runtime. No .unity or .prefab file is touched.
/// </summary>
public class PlaySuperTouchpoint_Home : MonoBehaviour
{
    // Fetch key, and the utm_content the store sees.
    private const string TouchpointName = "home";
    private const string MenuSceneName = "menu";

    // Accepted placement: normalized 0-1 with a TOP-LEFT origin.
    private static readonly Rect Bbox = new Rect(0.435625f, 0.7642276f, 0.121875f, 0.0609756f);

    private static PlaySuperTouchpoint_Home current;

    private GameObject overlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            Mount();
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            Mount();
        }
    }

    private static void Mount()
    {
        if (current != null)
        {
            return;
        }

        GameObject host = new GameObject("PlaySuperTouchpoint_Home");
        current = host.AddComponent<PlaySuperTouchpoint_Home>();
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
            return; // fail silent-and-absent: the menu must never depend on this
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
    }

    private void Build(Texture2D art, string action)
    {
        // A runtime canvas needs an EventSystem in the scene or taps silently do nothing.
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        overlay = new GameObject("PlaySuperTouchpointCanvas_Home", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

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

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }

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
