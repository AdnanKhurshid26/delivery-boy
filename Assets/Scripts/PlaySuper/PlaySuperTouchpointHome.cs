using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlaySuperUnity;

/// <summary>
/// Renders the "home" PlaySuper touchpoint on the menu screen.
///
/// PATTERN: home-entry-widget. The live touchpoint is a single ASSET node carrying one image and
/// a cta.action - and nothing else. No title, subtitle, badge, cta.text, cta.backgroundImage,
/// rewardId, dynamicConfig or popup subtree. So there is nothing to composite: the art is drawn
/// as-is and the whole widget is the tap target.
///
/// This code is written for THAT shape deliberately. If the touchpoint is later reconfigured to
/// carry copy or a separate CTA pill, this renderer needs extending - it will not pick those up
/// on its own.
///
/// The touchpoint is FETCHED LIVE every time the menu opens, so re-configuring it in PlaySuper
/// Studio changes the game without a rebuild. Nothing about the response is hardcoded here
/// except the fetch key and the accepted placement.
///
/// Replaces the placeholder store button from the core SDK setup - see the tombstone in
/// PlaySuperStoreEntry.cs. The store-open/close audio ducking that file used to own now lives
/// at the bottom of this one.
/// </summary>
public class PlaySuperTouchpointHome : MonoBehaviour
{
    // Fetch key, and the utm_content on every click. Matches the touchpoint's name in PlaySuper.
    private const string TouchpointName = "home";

    // First entry in EditorBuildSettings, i.e. the boot/menu scene. The touchpoint's name and the
    // accepted placement's screenshot both point here.
    private const string MenuSceneName = "menu";

    /// <summary>
    /// The accepted placement from PlaySuper Studio: normalized 0-1 with a TOP-LEFT origin.
    /// Change this one line to re-place the widget.
    /// </summary>
    private static readonly Rect Placement = new Rect(0.431875f, 0.5176152f, 0.131875f, 0.0447154f);

    private string ctaAction;
    private bool audioWasPausedBeforeStore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySpawnFor(SceneManager.GetActiveScene().name);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawnFor(scene.name);
    }

    private static void TrySpawnFor(string sceneName)
    {
        if (sceneName != MenuSceneName)
        {
            return;
        }

        // Not DontDestroyOnLoad, so any previous instance died with the old scene; this only
        // guards against a double spawn within one scene load.
        if (FindObjectOfType<PlaySuperTouchpointHome>() != null)
        {
            return;
        }

        new GameObject("PlaySuperTouchpointHome").AddComponent<PlaySuperTouchpointHome>();
    }

    private void OnEnable()
    {
        // OnStoreOpened / OnStoreClosed are STATIC Actions and must be qualified by the TYPE.
        // Through .Instance this is error CS0176 - easy to write by accident, because the
        // OpenStore call further down DOES go through .Instance.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private void OnDisable()
    {
        // Static events outlive this component and would keep a dead object alive otherwise.
        PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
    }

    private async void Start()
    {
        try
        {
            await BuildAsync();
        }
        catch (Exception e)
        {
            // Fail silent-and-absent: a touchpoint must never break the screen it sits on.
            Debug.LogWarning("[PlaySuper] home touchpoint failed to render: " + e.Message);
        }
    }

    private async Task BuildAsync()
    {
        // Every TouchpointManager call returns null on ANY error - they Debug.LogError and never
        // throw - so each step below is checked rather than assumed.
        TouchpointResponse touchpoint =
            await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperBootstrap.RewardCoinId);

        if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
        {
            return;
        }

        TouchpointNode node = touchpoint.nodes[0];
        if (node == null || node.images == null || node.images.Length == 0 || string.IsNullOrEmpty(node.images[0]))
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

        // The player can leave the menu while the art is downloading, which destroys this
        // component mid-await. Unity's overloaded null check catches that.
        if (this == null)
        {
            return;
        }

        // Passed through UNCHANGED when the widget is tapped - the API already appended
        // utm_content, so touching this string would corrupt attribution.
        ctaAction = node.cta.action;

        BuildWidget(art);
    }

    private void BuildWidget(Texture2D art)
    {
        GameObject canvasGo = new GameObject("PlaySuperTouchpointCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        EnsureEventSystem();

        // Container anchored to the accepted bbox. The bbox has a TOP-LEFT origin while uGUI
        // anchors run bottom-up, hence the 1 - y flip.
        GameObject regionGo = new GameObject("Region");
        regionGo.transform.SetParent(canvasGo.transform, false);

        RectTransform region = regionGo.AddComponent<RectTransform>();
        region.anchorMin = new Vector2(Placement.x, 1f - Placement.y - Placement.height);
        region.anchorMax = new Vector2(Placement.x + Placement.width, 1f - Placement.y);
        region.offsetMin = Vector2.zero;
        region.offsetMax = Vector2.zero;

        // FIELD-VERIFIED PITFALL: AspectRatioFitter.FitInParent refits to its PARENT and ignores
        // sub-region anchors. So the fitter must NOT go on the region rect above - the art gets
        // its own child rect that fills the region, with the RawImage + fitter on that.
        GameObject artGo = new GameObject("Art");
        artGo.transform.SetParent(regionGo.transform, false);

        RawImage raw = artGo.AddComponent<RawImage>();
        raw.texture = art;

        RectTransform artRect = artGo.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        AspectRatioFitter fitter = artGo.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = art.height == 0 ? 1f : (float)art.width / art.height;

        // home-entry-widget: persistent, no close control, the whole widget is the tap target.
        // This node carries no cta.text and no cta.backgroundImage, so nothing is drawn on top -
        // the card art is pre-baked and must never have generated text overlaid on it.
        Button button = artGo.AddComponent<Button>();
        button.targetGraphic = raw;
        button.onClick.AddListener(OpenStore);
    }

    /// <summary>
    /// A uGUI Canvas needs an EventSystem in the scene or clicks silently do nothing.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private void OpenStore()
    {
        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK instance is null; cannot open the store.");
                return;
            }

            // OpenStore, never Application.OpenURL: it uses the SDK's in-app WebView and syncs
            // analytics. ctaAction is passed through exactly as the API returned it.
            sdk.OpenStore(ctaAction);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
        }
    }

    // --- Store audio ducking, inherited from the retired PlaySuperStoreEntry placeholder ---

    private void HandleStoreOpened()
    {
        // The store is a full-screen native WebView over the menu, so duck the menu music
        // rather than leaving it playing underneath.
        audioWasPausedBeforeStore = AudioListener.pause;
        AudioListener.pause = true;
    }

    private void HandleStoreClosed()
    {
        // Restore rather than force-unpause, so a menu that was already muted stays muted.
        AudioListener.pause = audioWasPausedBeforeStore;
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
                Debug.LogWarning("[PlaySuper] touchpoint art download failed: " + request.error);
                return null;
            }

            return DownloadHandlerTexture.GetContent(request);
        }
    }
}
