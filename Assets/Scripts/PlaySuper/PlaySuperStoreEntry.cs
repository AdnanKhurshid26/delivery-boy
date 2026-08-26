using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the game's store presence on the menu screen.
///
/// The real entry point is the "reward-1" touchpoint -- the OFFER button positioned by its
/// accepted placement. This class mounts that renderer when the menu loads and tears it
/// down when the player leaves.
///
/// The plain REWARDS button is now only a FALLBACK, built if the touchpoint cannot be
/// shown. Without it, a failed fetch would leave the game with no way into the store.
///
/// Nothing here edits menu.unity -- every object is created in code.
/// </summary>
public class PlaySuperStoreEntry : MonoBehaviour
{
    // First entry in EditorBuildSettings, i.e. the boot scene.
    private const string MenuSceneName = "menu";

    private GameObject touchpointHost;
    private GameObject fallbackCanvas;
    private float timeScaleBeforeStore = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        var host = new GameObject("PlaySuperStoreEntry");
        DontDestroyOnLoad(host);
        host.AddComponent<PlaySuperStoreEntry>();
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshForScene(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        // These events are static -- they hang off the type, not the instance. Writing
        // PlaySuperUnitySDK.Instance.OnStoreOpened is a compile error (CS0176).
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private void OnDisable()
    {
        // Static events outlive this component and would keep a dead object alive.
        PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshForScene(scene);
    }

    private void RefreshForScene(Scene scene)
    {
        bool onMenu = scene.name == MenuSceneName;

        if (onMenu)
        {
            if (touchpointHost == null)
            {
                MountTouchpoint();
            }

            return;
        }

        if (touchpointHost != null)
        {
            Destroy(touchpointHost);
            touchpointHost = null;
        }

        if (fallbackCanvas != null)
        {
            Destroy(fallbackCanvas);
            fallbackCanvas = null;
        }
    }

    private void MountTouchpoint()
    {
        // A runtime canvas is inert without one -- clicks silently do nothing. Do this
        // before the touchpoint builds its own canvas.
        EnsureEventSystem();

        touchpointHost = new GameObject("PlaySuperTouchpoint_reward-1");
        touchpointHost.transform.SetParent(transform, false);

        var renderer = touchpointHost.AddComponent<PlaySuperTouchpointReward1>();
        renderer.OnUnavailable = BuildFallbackButton;
    }

    private bool IsOnMenu()
    {
        return SceneManager.GetActiveScene().name == MenuSceneName;
    }

    /// <summary>
    /// Plain store button, built only when the touchpoint could not be shown.
    /// </summary>
    private void BuildFallbackButton()
    {
        // The fetch may have resolved after the player already started a level -- do not
        // paint a menu button onto the gameplay scene.
        if (fallbackCanvas != null || !IsOnMenu())
        {
            return;
        }

        EnsureEventSystem();

        fallbackCanvas = new GameObject("PlaySuperStoreCanvas");
        fallbackCanvas.transform.SetParent(transform, false);

        var canvas = fallbackCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = fallbackCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        fallbackCanvas.AddComponent<GraphicRaycaster>();

        var buttonObject = new GameObject("StoreButton", typeof(RectTransform));
        buttonObject.transform.SetParent(fallbackCanvas.transform, false);

        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(320f, 110f);

        var background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.11f, 0.09f, 0.30f, 0.92f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(OpenStore);

        var labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.fontSize = 40f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        var eventSystem = new GameObject("EventSystem",
            typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystem);
    }

    private void OpenStore()
    {
        if (PlaySuperUnitySDK.Instance == null)
        {
            Debug.LogWarning("[PlaySuper] SDK instance missing -- store not opened.");
            return;
        }

        // Always the SDK's in-app WebView, never Application.OpenURL -- OpenStore carries
        // the auth token and syncs analytics.
        PlaySuperUnitySDK.Instance.OpenStore();
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
}
