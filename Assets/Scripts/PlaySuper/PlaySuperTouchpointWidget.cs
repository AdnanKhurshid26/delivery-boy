using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Shared compositing for the "home-entry-widget" pattern: one leaf ASSET node made of
/// a single baked image plus a CTA, where the whole widget is the tap target and there
/// is no close control. Subclasses supply their OWN touchpoint name, placement bbox and
/// host canvas sorting order -- one renderer, one placement, never shared.
///
/// MOUNT THIS ON THE PANEL THAT IS THE SCREEN (panel.AddComponent&lt;T&gt;()), so the
/// panel's own SetActive drives OnEnable/OnDisable and the widget cannot appear on a
/// sibling screen. No scene or prefab changes either way.
/// </summary>
public abstract class PlaySuperTouchpointWidget : MonoBehaviour
{
    /// <summary>Lowercase-kebab touchpoint name; also the utm_content fetch key.</summary>
    protected abstract string TouchpointName { get; }

    /// <summary>
    /// THIS touchpoint's own approved placement: normalized 0-1 with a TOP-LEFT origin,
    /// as drawn on the screenshot.
    /// </summary>
    protected abstract Rect Bbox { get; }

    /// <summary>
    /// Sorting order of the canvas the host screen lives on, read from the .unity scene.
    /// The widget draws at this + 1: enough to beat its own screen, not enough to float
    /// over a pause menu or dialog.
    /// </summary>
    protected abstract int HostCanvasSortingOrder { get; }

    Canvas widgetCanvas;
    Texture2D artTexture;
    bool building;

    // OnEnable, not Start: a panel shown a second time never re-runs Start.
    async void OnEnable()
    {
        // OnEnable can fire again before the first download finishes.
        if (building)
        {
            return;
        }

        building = true;

        try
        {
            await Build();
        }
        finally
        {
            building = false;
        }
    }

    void OnDisable()
    {
        Teardown();
    }

    void OnDestroy()
    {
        Teardown();
    }

    void Teardown()
    {
        if (widgetCanvas != null)
        {
            Destroy(widgetCanvas.gameObject);
            widgetCanvas = null;
        }

        // UnityWebRequestTexture textures are not collected for you.
        if (artTexture != null)
        {
            Destroy(artTexture);
            artTexture = null;
        }
    }

    async Task Build()
    {
        TouchpointResponse touchpoint =
            await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperBootstrap.CoinId);

        // Fail silent-and-absent: a touchpoint that fails to load must never block or
        // break the screen it sits on.
        if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
        {
            return;
        }

        TouchpointNode node = touchpoint.nodes[0];

        if (node == null || node.images == null || node.images.Length == 0)
        {
            return;
        }

        if (node.cta == null || string.IsNullOrEmpty(node.cta.action))
        {
            return;
        }

        // The screen may have been left while the fetch was in flight.
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        Texture2D texture = await Download(node.images[0]);

        if (texture == null)
        {
            return;
        }

        if (this == null || !isActiveAndEnabled)
        {
            Destroy(texture);
            return;
        }

        // A rebuild raced us; keep the newest and drop this one.
        if (widgetCanvas != null)
        {
            Destroy(texture);
            return;
        }

        artTexture = texture;
        Compose(node.cta.action);
    }

    void Compose(string ctaAction)
    {
        // ROOT-level canvas, not nested: a nested canvas takes its parent's rect, and
        // the bbox is normalized against the WHOLE screen.
        var canvasGo = new GameObject("PlaySuperTouchpoint_" + TouchpointName);
        canvasGo.transform.SetParent(null, false);

        widgetCanvas = canvasGo.AddComponent<Canvas>();
        widgetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        widgetCanvas.sortingOrder = ResolveSortingOrder();
        canvasGo.AddComponent<GraphicRaycaster>();

        // A uGUI canvas needs an EventSystem in the scene or clicks silently do nothing.
        // Both of this project's UI scenes already have one; this is belt and braces.
        if (FindObjectOfType<EventSystem>() == null)
        {
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();
        }

        // Container rect anchored to the sub-region. The bbox has a TOP-LEFT origin
        // (y grows downward) while RectTransform anchors are BOTTOM-LEFT, so y is
        // flipped -- getting this wrong mirrors the widget vertically, which looks
        // plausible mid-screen and obviously wrong near an edge.
        var anchorGo = new GameObject("Anchor", typeof(RectTransform));
        anchorGo.transform.SetParent(canvasGo.transform, false);

        var anchorRect = anchorGo.GetComponent<RectTransform>();
        anchorRect.anchorMin = new Vector2(Bbox.x, 1f - Bbox.y - Bbox.height);
        anchorRect.anchorMax = new Vector2(Bbox.x + Bbox.width, 1f - Bbox.y);
        anchorRect.offsetMin = Vector2.zero;
        anchorRect.offsetMax = Vector2.zero;

        // The image goes INSIDE that container, because AspectRatioFitter.FitInParent
        // refits to its PARENT and ignores sub-region anchors.
        var artGo = new GameObject("Art", typeof(RectTransform));
        artGo.transform.SetParent(anchorGo.transform, false);

        var artRect = artGo.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        var rawImage = artGo.AddComponent<RawImage>();
        rawImage.texture = artTexture;
        rawImage.raycastTarget = true;

        var fitter = artGo.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)artTexture.width / artTexture.height;

        // Whole widget is the tap target -- this pattern has no separate CTA art and no
        // cta.text to draw, so the baked image is the button.
        var button = artGo.AddComponent<Button>();
        button.targetGraphic = rawImage;

        string action = ctaAction;
        button.onClick.AddListener(() =>
        {
            if (PlaySuperUnitySDK.Instance == null)
            {
                return;
            }

            // Never Application.OpenURL: OpenStore uses the SDK's in-app WebView and
            // syncs analytics. The action is passed through UNCHANGED (utm_content is
            // already appended server-side).
            PlaySuperUnitySDK.Instance.OpenStore(action);
        });
    }

    /// <summary>
    /// Floor the sorting order at runtime: the highest order at or below the host
    /// screen's, plus one. In this project every game canvas sits at 0, so this resolves
    /// to 1 -- but reading it beats hardcoding a number that is only right by accident.
    /// </summary>
    int ResolveSortingOrder()
    {
        int highest = HostCanvasSortingOrder;

        // Include inactive: a hidden panel's canvas still counts.
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);

        foreach (Canvas candidate in canvases)
        {
            if (candidate == null || candidate == widgetCanvas)
            {
                continue;
            }

            if (candidate.sortingOrder <= HostCanvasSortingOrder &&
                candidate.sortingOrder > highest)
            {
                highest = candidate.sortingOrder;
            }
        }

        return highest + 1;
    }

    static async Task<Texture2D> Download(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                await Task.Yield();
            }

            return request.result == UnityWebRequest.Result.Success
                ? DownloadHandlerTexture.GetContent(request)
                : null;
        }
    }
}
