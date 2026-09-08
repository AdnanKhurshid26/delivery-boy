using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

// Shared plumbing for this game's touchpoint renderers.
//
// Both live touchpoints are the home-entry-widget pattern: one leaf node carrying a single
// image plus a cta.action, with no title, subtitle, badge, cta.text or cta.backgroundImage.
// So the whole widget is the tap target, there is nothing to composite on top of the art,
// and no branches are written for fields these touchpoints never carry.
//
// The hydrated config is fetched live on every screen entry - no response values are baked
// into constants, so the studio can swap the artwork without a client release.
public abstract class PlaySuperTouchpointWidget : MonoBehaviour
{
    // Fetch key; also the utm_content on the CTA.
    protected abstract string TouchpointName { get; }

    // The studio's accepted placement, normalized 0-1 with a TOP-LEFT origin.
    protected abstract Rect Bbox { get; }

    const int CanvasSortingOrder = 900;

    bool built;

    protected virtual void OnEnable()
    {
        // These events are STATIC on the type. Subscribing through .Instance is CS0176.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    protected virtual void OnDisable()
    {
        // Static events outlive the component, so always unsubscribe.
        PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;

        AudioListener.pause = false;
    }

    protected async void Start()
    {
        if (built)
        {
            return;
        }
        built = true;

        // Every SDK call returns null on any error and never throws, so null-check the lot
        // and fail silent-and-absent: a touchpoint must never break the screen it sits on.
        TouchpointResponse touchpoint = await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperBootstrap.CoinId);
        if (this == null)
        {
            return;
        }
        if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
        {
            return;
        }

        TouchpointNode node = touchpoint.nodes[0];
        if (node == null || node.images == null || node.images.Length == 0)
        {
            return;
        }

        Texture2D art = await Download(node.images[0]);
        if (this == null || art == null)
        {
            return;
        }

        string action = node.cta != null ? node.cta.action : null;
        Build(art, action);
    }

    void Build(Texture2D art, string action)
    {
        // Canvas is a child of this component's object, so the widget lives and hides with
        // whatever mounted it.
        GameObject canvasObject = new GameObject("PlaySuperCanvas_" + TouchpointName);
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;
        canvasObject.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        // Container anchored to the accepted bbox. Top-left origin converts to uGUI as
        // anchorMin = (x, 1 - y - h), anchorMax = (x + w, 1 - y).
        GameObject containerObject = new GameObject("Widget", typeof(RectTransform));
        RectTransform container = containerObject.GetComponent<RectTransform>();
        container.SetParent(canvasObject.transform, false);
        container.anchorMin = new Vector2(Bbox.x, 1f - Bbox.y - Bbox.height);
        container.anchorMax = new Vector2(Bbox.x + Bbox.width, 1f - Bbox.y);
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;

        // The art goes INSIDE that container with its own fitter. AspectRatioFitter's
        // FitInParent refits to the PARENT rect and ignores sub-region anchors, so the
        // fitter must never sit on the rect that carries the placement anchors.
        GameObject artObject = new GameObject("Art", typeof(RectTransform));
        RectTransform artRect = artObject.GetComponent<RectTransform>();
        artRect.SetParent(container, false);
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        RawImage image = artObject.AddComponent<RawImage>();
        image.texture = art;

        AspectRatioFitter fitter = artObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = art.height == 0 ? 1f : (float)art.width / art.height;

        Button button = artObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(delegate { OpenStore(action); });
    }

    static void OpenStore(string action)
    {
        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null || string.IsNullOrEmpty(action))
        {
            return;
        }

        // In-app WebView store, and it syncs analytics. Never Application.OpenURL.
        // The action is passed through unchanged - utm_content is already appended
        // server-side.
        sdk.OpenStore(action);
    }

    // A runtime-built uGUI canvas needs an EventSystem in the scene or taps silently do
    // nothing, and this project's scenes are not guaranteed to have one.
    static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    static void HandleStoreOpened()
    {
        AudioListener.pause = true;
    }

    static void HandleStoreClosed()
    {
        AudioListener.pause = false;
    }

    static async Task<Texture2D> Download(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return null;
            }

            return DownloadHandlerTexture.GetContent(request);
        }
    }
}
