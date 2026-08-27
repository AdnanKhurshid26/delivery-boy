using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Renders the "home" PlaySuper touchpoint on the main menu scene.
///
/// Pattern: home-entry-widget - one leaf node carrying art plus a CTA, no
/// children, no close control. The whole pill is the tap target.
///
/// The art (a pixel "OFFER" pill) is pre-baked: this touchpoint carries no
/// cta.text and no cta.backgroundImage, so nothing is composited on top of it.
/// Placement puts it in the menu's button stack, between "Start game" and
/// "Settings".
///
/// Built entirely at runtime - menu.unity is never edited.
/// </summary>
public class PlaySuperTouchpoint_Home : MonoBehaviour
{
    /// <summary>Fetch key, also the utm_content on the CTA.</summary>
    private const string TouchpointName = "home";

    private const string MenuSceneName = "menu";

    /// <summary>
    /// Accepted placement, normalized 0-1 with a top-left origin, as measured on
    /// the reviewed screenshot of the menu screen.
    /// </summary>
    private static readonly Rect Bbox =
        new Rect(0.431875f, 0.5176152f, 0.131875f, 0.0447154f);

    private GameObject canvasRoot;
    private float timeScaleBeforeStore = 1f;

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

        // STATIC events - qualified by the type. Going through .Instance is CS0176.
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
        // AfterSceneLoad means the menu is already up on a cold start, so
        // sceneLoaded never fires for it.
        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            LoadAndBuild();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            LoadAndBuild();
        }
        else
        {
            DestroyWidget();
        }
    }

    /// <summary>
    /// Fetches live on every menu entry and fails silent-and-absent: if anything
    /// is missing the menu simply renders without the widget. A touchpoint must
    /// never block or break the screen it sits on.
    /// </summary>
    private async void LoadAndBuild()
    {
        if (canvasRoot != null)
        {
            return;
        }

        TouchpointResponse touchpoint =
            await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperConfig.RewardCoinId);

        // Every TouchpointManager call returns null on ANY error - it logs and
        // never throws.
        if (touchpoint?.nodes == null || touchpoint.nodes.Length == 0)
        {
            return;
        }

        TouchpointNode node = touchpoint.nodes[0];
        if (node?.images == null || node.images.Length == 0 || string.IsNullOrEmpty(node.images[0]))
        {
            return;
        }

        string action = node.cta != null ? node.cta.action : null;

        Texture2D art = await Download(node.images[0]);
        if (art == null)
        {
            return;
        }

        // The scene may have changed while the fetch and download were in flight.
        if (this == null || SceneManager.GetActiveScene().name != MenuSceneName)
        {
            return;
        }

        BuildWidget(art, action);
    }

    private void BuildWidget(Texture2D art, string action)
    {
        if (canvasRoot != null)
        {
            return;
        }

        EnsureEventSystem();

        canvasRoot = new GameObject("PlaySuperHomeTouchpointCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        // No CanvasScaler: the widget is sized from the live pixel rect below, so
        // scaling it a second time would fight the placement.
        canvasRoot.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasRoot.GetComponent<RectTransform>();

        GameObject widget = new GameObject("OfferWidget");
        widget.transform.SetParent(canvasRoot.transform, false);

        RectTransform rect = widget.AddComponent<RectTransform>();

        // Anchor to the CENTRE of the accepted bbox and derive the size from the
        // art's own aspect ratio. Stretching the rect to the raw bbox instead
        // would distort the pill on any screen whose aspect differs from the
        // reviewed screenshot.
        float centerX = Bbox.x + (Bbox.width * 0.5f);
        float centerYFromTop = Bbox.y + (Bbox.height * 0.5f);

        rect.anchorMin = new Vector2(centerX, 1f - centerYFromTop);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;

        float height = canvasRect.rect.height * Bbox.height;
        float aspect = art.height > 0 ? (float)art.width / art.height : 1f;
        rect.sizeDelta = new Vector2(height * aspect, height);

        RawImage image = widget.AddComponent<RawImage>();
        image.texture = art;

        // Whole widget is the tap target - this pattern has no separate button.
        Button button = widget.AddComponent<Button>();
        button.targetGraphic = image;

        if (!string.IsNullOrEmpty(action))
        {
            // Pass cta.action through UNCHANGED: utm_content is already appended
            // server-side. Always the in-app WebView, never Application.OpenURL.
            button.onClick.AddListener(() =>
            {
                if (PlaySuperUnitySDK.Instance == null)
                {
                    return;
                }

                PlaySuperUnitySDK.Instance.OpenStore(action);
            });
        }
        else
        {
            button.interactable = false;
        }
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

    private void HandleStoreOpened()
    {
        timeScaleBeforeStore = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private void HandleStoreClosed()
    {
        Time.timeScale = timeScaleBeforeStore <= 0f ? 1f : timeScaleBeforeStore;
        AudioListener.pause = false;
    }

    /// <summary>
    /// A uGUI Canvas needs an EventSystem or taps silently do nothing.
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

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("[PlaySuper] Touchpoint art failed to download: " + request.error);
                return null;
            }

            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture != null)
            {
                // Pixel art: keep the edges crisp instead of bilinear-blurred.
                texture.filterMode = FilterMode.Point;
            }

            return texture;
        }
    }
}
