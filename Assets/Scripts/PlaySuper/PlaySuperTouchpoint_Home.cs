using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders the "home" PlaySuper touchpoint on the menu scene.
///
/// Pattern: home-entry-widget. The live response carries exactly ONE leaf node -
/// a pre-baked "OFFER" pill in images[0] plus a cta.action - with no title,
/// no cta.text and no separate button art. Nothing is composited on top of the
/// art; the whole widget is the tap target.
///
/// Built at runtime on its own overlay canvas, so menu.unity is never edited.
/// This also owns the store-open/close ducking that used to live in
/// PlaySuperStoreEntry, the temporary button this widget replaces.
/// </summary>
public class PlaySuperTouchpoint_Home : MonoBehaviour
{
    /// <summary>Fetch key - also the utm_content on the CTA.</summary>
    private const string TouchpointName = "home";

    private const string MenuSceneName = "menu";

    /// <summary>
    /// The accepted placement, normalized 0-1 with a TOP-LEFT origin: the OFFER
    /// pill sits in the menu's button stack, between Start game and Settings.
    /// </summary>
    private static readonly Rect Placement = new Rect(
        0.431875f,
        0.5176151761517616f,
        0.131875f,
        0.04471544715447155f);

    private GameObject canvasRoot;
    private float timeScaleBeforeStore = 1f;
    private bool building;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        GameObject host = new GameObject("PlaySuperTouchpoint_Home");
        DontDestroyOnLoad(host);
        host.AddComponent<PlaySuperTouchpoint_Home>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // These events are STATIC - qualified by the type, never by .Instance
        // (that is CS0176). They outlive this component, so they are
        // unsubscribed in OnDisable or they keep a dead object alive.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
    }

    private void Start()
    {
        // AfterSceneLoad means the menu scene is already up on a cold start, so
        // sceneLoaded will not fire for it.
        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            _ = BuildAsync();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            _ = BuildAsync();
        }
        else
        {
            DestroyWidget();
        }
    }

    private void HandleStoreOpened()
    {
        timeScaleBeforeStore = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void HandleStoreClosed()
    {
        // Restore game state on close rather than assuming a timescale of 1.
        Time.timeScale = timeScaleBeforeStore <= 0f ? 1f : timeScaleBeforeStore;
        AudioListener.pause = false;
    }

    /// <summary>
    /// Fetches the touchpoint live and builds the widget. Every failure path
    /// returns silently: a touchpoint that cannot load must leave the menu
    /// exactly as it would have been without it.
    /// </summary>
    private async Task BuildAsync()
    {
        if (canvasRoot != null || building)
        {
            return;
        }

        building = true;

        try
        {
            // GetTouchpointByName returns null on ANY error - it logs and never
            // throws - so the result is checked rather than trusted. coinId is
            // required, or the API returns the raw editing view.
            TouchpointResponse touchpoint =
                await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperConfig.RewardCoinId);

            if (touchpoint?.nodes == null || touchpoint.nodes.Length == 0)
            {
                return;
            }

            TouchpointNode node = touchpoint.nodes[0];

            string artUrl = node.images != null && node.images.Length > 0 ? node.images[0] : null;
            string action = node.cta?.action;

            if (string.IsNullOrEmpty(artUrl) || string.IsNullOrEmpty(action))
            {
                return;
            }

            Texture2D art = await Download(artUrl);
            if (art == null)
            {
                return;
            }

            // The player can leave the menu while the fetch and download are in
            // flight, and this component can be torn down under it.
            if (this == null || canvasRoot != null || SceneManager.GetActiveScene().name != MenuSceneName)
            {
                return;
            }

            Build(art, action);
        }
        finally
        {
            building = false;
        }
    }

    private void Build(Texture2D art, string action)
    {
        EnsureEventSystem();

        canvasRoot = new GameObject("PlaySuperHomeTouchpointCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        // The placement box gets its OWN container rect. AspectRatioFitter's
        // FitInParent refits to the PARENT rect and ignores the sub-region
        // anchors, so putting the fitter directly on a rect anchored to the box
        // would size it against the whole screen instead.
        GameObject slot = new GameObject("PlacementSlot");
        slot.transform.SetParent(canvasRoot.transform, false);

        RectTransform slotRect = slot.AddComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(Placement.x, 1f - Placement.y - Placement.height);
        slotRect.anchorMax = new Vector2(Placement.x + Placement.width, 1f - Placement.y);
        slotRect.offsetMin = Vector2.zero;
        slotRect.offsetMax = Vector2.zero;

        GameObject widget = new GameObject("OfferWidget");
        widget.transform.SetParent(slot.transform, false);

        RectTransform widgetRect = widget.AddComponent<RectTransform>();
        widgetRect.anchorMin = new Vector2(0.5f, 0.5f);
        widgetRect.anchorMax = new Vector2(0.5f, 0.5f);
        widgetRect.pivot = new Vector2(0.5f, 0.5f);
        widgetRect.anchoredPosition = Vector2.zero;

        RawImage image = widget.AddComponent<RawImage>();
        image.texture = art;

        // The art is pre-baked, so it keeps its own aspect inside the placement
        // box instead of being stretched to it.
        AspectRatioFitter fitter = widget.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)art.width / art.height;

        // home-entry-widget: the whole widget is the tap target, there is no
        // separate CTA button to composite.
        Button button = widget.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => OpenStore(action));
    }

    private void OpenStore(string action)
    {
        // Always the SDK's in-app WebView, never Application.OpenURL - OpenStore
        // carries the session and syncs analytics. The action is passed through
        // unchanged; utm params are already appended server-side.
        if (PlaySuperUnitySDK.Instance == null)
        {
            Debug.LogWarning("[PlaySuper] Offer tapped before the SDK was ready.");
            return;
        }

        PlaySuperUnitySDK.Instance.OpenStore(action);
    }

    private void DestroyWidget()
    {
        if (canvasRoot == null)
        {
            return;
        }

        Destroy(canvasRoot);
        canvasRoot = null;
    }

    /// <summary>
    /// A uGUI Canvas needs an EventSystem in the scene or clicks silently do
    /// nothing - never assume the scene already has one.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(eventSystem);
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

            return request.result == UnityWebRequest.Result.Success
                ? DownloadHandlerTexture.GetContent(request)
                : null;
        }
    }
}
