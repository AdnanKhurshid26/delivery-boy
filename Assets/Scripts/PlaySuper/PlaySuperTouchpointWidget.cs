using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Shared runtime compositing for this game's PlaySuper touchpoints.
///
/// Both of Agentic Game's live touchpoints are the "home-entry-widget" pattern: a single
/// leaf node carrying one pre-baked image plus a cta.action, with no children, no rotation
/// and no dynamic config. There is nothing to composite - no cta.text, no separate CTA pill,
/// no background object - so this class deliberately contains NO code for those fields.
/// The whole widget is the tap target; the pattern has no close control.
///
/// Everything is built at runtime in a ScreenSpaceOverlay canvas. No .unity or .prefab file
/// is ever touched.
/// </summary>
public abstract class PlaySuperTouchpointWidget : MonoBehaviour
{
    /// <summary>Lowercase-kebab touchpoint name. This is the SDK fetch key AND the utm_content.</summary>
    protected abstract string TouchpointName { get; }

    /// <summary>
    /// Accepted placement, normalized 0-1 with a TOP-LEFT origin, exactly as it arrives from
    /// PlaySuper.
    /// </summary>
    protected abstract Rect Placement { get; }

    /// <summary>Above the game's own UI, below anything modal the game may add later.</summary>
    protected virtual int SortingOrder
    {
        get { return 900; }
    }

    private GameObject canvasRoot;
    private Texture2D artTexture;
    private string ctaAction;
    private bool building;

    private void OnEnable()
    {
        // OnStoreOpened / OnStoreClosed are STATIC Actions - subscribed on the TYPE, never
        // through .Instance (that is CS0176). Static events outlive the component, so the
        // same handlers are removed in OnDisable.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;

        // Built from OnEnable rather than Start so the widget rebuilds correctly if its host
        // panel is hidden and shown again.
        _ = BuildAsync();
    }

    private void OnDisable()
    {
        PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
        Teardown();
    }

    private void OnDestroy()
    {
        Teardown();
    }

    private void Teardown()
    {
        if (canvasRoot != null)
        {
            Destroy(canvasRoot);
            canvasRoot = null;
        }

        if (artTexture != null)
        {
            Destroy(artTexture);
            artTexture = null;
        }
    }

    private async Task BuildAsync()
    {
        if (building || canvasRoot != null)
        {
            return;
        }

        building = true;

        try
        {
            // Live fetch. The hydrated JSON from the connector was design-time context for
            // choosing this pattern - it is NOT embedded here, so the creative can be changed
            // in PlaySuper Studio without another build.
            TouchpointResponse touchpoint = null;
            try
            {
                touchpoint = await TouchpointManager.GetTouchpointByName(
                    TouchpointName, PlaySuperBootstrap.CoinId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] Touchpoint '" + TouchpointName + "' fetch threw: " + e.Message);
                return;
            }

            // Fetches return null on ANY error (they log, they never throw), so every step
            // below fails silent-and-absent: the widget simply does not appear. A touchpoint
            // must never block or break the screen it sits on.
            if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
            {
                return;
            }

            TouchpointNode node = touchpoint.nodes[0];
            if (node == null || node.images == null || node.images.Length == 0 ||
                string.IsNullOrEmpty(node.images[0]))
            {
                return;
            }

            if (node.cta == null || string.IsNullOrEmpty(node.cta.action))
            {
                return;
            }

            ctaAction = node.cta.action;

            Texture2D art = await Download(node.images[0]);
            if (art == null)
            {
                return;
            }

            // The host panel or scene may have gone away while the download was in flight.
            if (this == null || !isActiveAndEnabled)
            {
                Destroy(art);
                return;
            }

            artTexture = art;
            BuildWidget(art);
        }
        finally
        {
            building = false;
        }
    }

    private void BuildWidget(Texture2D art)
    {
        EnsureEventSystem();

        // Root-level canvas, torn down in OnDisable. Kept out of the host panel's hierarchy so
        // it is a true overlay rather than a nested canvas inheriting someone else's sorting.
        canvasRoot = new GameObject("PlaySuperTouchpoint_" + TouchpointName);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        // The placement bbox is normalized with a TOP-LEFT origin; uGUI anchors are
        // bottom-left, hence the y flip.
        GameObject container = new GameObject("Widget");
        container.transform.SetParent(canvasRoot.transform, false);

        // A fully transparent Image gives the Button something to raycast against without
        // drawing anything over the art.
        Image hitArea = container.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);

        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(Placement.x, 1f - Placement.y - Placement.height);
        containerRect.anchorMax = new Vector2(Placement.x + Placement.width, 1f - Placement.y);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // PITFALL: AspectRatioFitter.FitInParent refits to its PARENT and ignores sub-region
        // anchors, so the RawImage and its fitter go INSIDE this container - never on it.
        GameObject artGo = new GameObject("Art");
        artGo.transform.SetParent(container.transform, false);

        RawImage raw = artGo.AddComponent<RawImage>();
        raw.texture = art;
        raw.raycastTarget = false;

        RectTransform artRect = artGo.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        AspectRatioFitter fitter = artGo.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)art.width / art.height;

        // Whole widget is the tap target - this pattern has no close control.
        Button button = container.AddComponent<Button>();
        button.targetGraphic = hitArea;
        button.onClick.AddListener(OpenStore);
    }

    private void OpenStore()
    {
        if (string.IsNullOrEmpty(ctaAction))
        {
            return;
        }

        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            Debug.LogWarning("[PlaySuper] SDK instance not ready - store not opened.");
            return;
        }

        // cta.action is passed through UNCHANGED: the API already appended utm_content, which
        // is how this placement stays attributable. In-app WebView, never Application.OpenURL.
        sdk.OpenStore(ctaAction);
    }

    // Audio ducking inherited from the retired core-setup store button. AudioListener.pause is
    // orthogonal to AudioListener.volume, so the player's own mute setting (applied by
    // VolumeSpawn) survives untouched.
    private void HandleStoreOpened()
    {
        AudioListener.pause = true;
    }

    private void HandleStoreClosed()
    {
        AudioListener.pause = false;
    }

    private static void EnsureEventSystem()
    {
        // A runtime uGUI canvas needs an EventSystem in the scene or taps silently do nothing.
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
                Debug.LogWarning("[PlaySuper] Touchpoint art download failed: " + request.error);
                return null;
            }

            return DownloadHandlerTexture.GetContent(request);
        }
    }
}
