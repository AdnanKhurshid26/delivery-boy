using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders the "reward-1" touchpoint on the main menu.
///
/// Pattern: home-entry-widget -- one leaf ASSET node, art plus a CTA, no children.
/// The art is a pre-baked pixel-art OFFER button with its label already in the
/// image, so the node carries cta.action ONLY (no cta.text, no cta.backgroundImage)
/// and nothing is composited on top of it.
///
/// Built at runtime and scene-gated to the menu, the same way PlaySuperStore
/// bootstraps, so no .unity or .prefab file is touched.
///
/// The response is fetched LIVE every time the menu loads -- the art, the CTA
/// target and the copy can all be changed server-side without a new build, so
/// none of it is embedded here.
/// </summary>
public class PlaySuperTouchpoint_Reward1 : MonoBehaviour
{
    private const string TouchpointName = "reward-1";
    private const string MenuSceneName = "menu";

    // The accepted placement, normalized 0-1 with a TOP-LEFT origin: the OFFER
    // slot in the menu button stack, between "Start game" and "Settings".
    private static readonly Rect Bbox =
        new Rect(0.431875f, 0.5176152f, 0.131875f, 0.0447154f);

    private static PlaySuperTouchpoint_Reward1 _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // AfterSceneLoad has already missed sceneLoaded for the first scene.
        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            Spawn();
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            Spawn();
        }
    }

    private static void Spawn()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject host = new GameObject("PlaySuperTouchpointReward1");
        _instance = host.AddComponent<PlaySuperTouchpoint_Reward1>();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private async void Start()
    {
        TouchpointResponse touchpoint;

        try
        {
            // coinId is required -- without it the API returns the raw editing
            // view rather than hydrated data. It lives on PlaySuperBootstrap;
            // never redeclare it.
            touchpoint = await TouchpointManager.GetTouchpointByName(
                TouchpointName, PlaySuperBootstrap.CoinId);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] reward-1 fetch failed: " + e.Message);
            return;
        }

        // Every SDK call returns null on error rather than throwing, and a
        // touchpoint must never break the screen it sits on -- so from here on
        // anything missing means render nothing at all, silently.
        if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
        {
            return;
        }

        TouchpointNode node = touchpoint.nodes[0];

        if (node == null || node.images == null || node.images.Length == 0
            || string.IsNullOrEmpty(node.images[0]))
        {
            return;
        }

        if (node.cta == null || string.IsNullOrEmpty(node.cta.action))
        {
            return;
        }

        Texture2D art = await Download(node.images[0]);

        if (art == null)
        {
            return;
        }

        // The player can leave the menu while the art is still downloading.
        if (this == null || gameObject == null)
        {
            return;
        }

        Build(art, node.cta.action);
    }

    private void Build(Texture2D art, string action)
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("PlaySuperTouchpointCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above the menu UI, and above PlaySuperStore's canvas (500).
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 738f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        // The bbox arrives with a top-left origin; uGUI anchors count from the
        // bottom, hence the y flip. Anchors only, no sizeDelta -- the widget
        // then keeps its proportions of the screen at any resolution.
        GameObject slotObject = new GameObject("OfferSlot");
        slotObject.transform.SetParent(canvasObject.transform, false);

        RectTransform slotRect = slotObject.AddComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(Bbox.x, 1f - Bbox.y - Bbox.height);
        slotRect.anchorMax = new Vector2(Bbox.x + Bbox.width, 1f - Bbox.y);
        slotRect.offsetMin = Vector2.zero;
        slotRect.offsetMax = Vector2.zero;

        // AspectRatioFitter.FitInParent refits to the PARENT rect and ignores
        // the anchors on its own object, so the art gets its own child rect
        // inside the slot rather than sharing one with it.
        GameObject artObject = new GameObject("OfferArt");
        artObject.transform.SetParent(slotObject.transform, false);

        // Point filtering keeps the pixel art crisp; the default bilinear
        // filter would blur it against the rest of the menu.
        art.filterMode = FilterMode.Point;
        art.wrapMode = TextureWrapMode.Clamp;

        RawImage image = artObject.AddComponent<RawImage>();
        image.texture = art;

        RectTransform artRect = artObject.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        AspectRatioFitter fitter = artObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)art.width / art.height;

        // Persistent entry point: no close control, the whole widget is the
        // tap target.
        Button button = artObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(delegate { OpenStore(action); });
    }

    private static void OpenStore(string action)
    {
        try
        {
            if (PlaySuperUnitySDK.Instance == null)
            {
                return;
            }

            // In-app WebView, and it syncs analytics -- never Application.OpenURL.
            // The action is passed through UNCHANGED: the API already appended
            // utm_content=reward-1 at hydration.
            PlaySuperUnitySDK.Instance.OpenStore(action);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] reward-1 OpenStore failed: " + e.Message);
        }
    }

    /// <summary>
    /// A uGUI Canvas without an EventSystem swallows clicks silently, so never
    /// assume the scene already has one. Cheap and idempotent -- PlaySuperStore
    /// does the same check and either may run first.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private static async Task<Texture2D> Download(string url)
    {
        try
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
                    Debug.LogWarning("[PlaySuper] reward-1 art download failed: " + request.error);
                    return null;
                }

                return DownloadHandlerTexture.GetContent(request);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] reward-1 art download failed: " + e.Message);
            return null;
        }
    }
}
