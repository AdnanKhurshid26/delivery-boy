using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The game's one way into the PlaySuper store: a button built at runtime on the menu
/// scene. Nothing here touches menu.unity -- the canvas, the button and (if needed) the
/// EventSystem are all created in code.
///
/// This is a plain placeholder. The positioned, art-directed entry point arrives later as
/// a touchpoint and replaces it.
/// </summary>
public class PlaySuperStoreEntry : MonoBehaviour
{
    // First entry in EditorBuildSettings, i.e. the boot scene.
    private const string MenuSceneName = "menu";

    private GameObject canvasObject;
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

        if (onMenu && canvasObject == null)
        {
            BuildButton();
        }
        else if (!onMenu && canvasObject != null)
        {
            Destroy(canvasObject);
            canvasObject = null;
        }
    }

    private void BuildButton()
    {
        EnsureEventSystem();

        canvasObject = new GameObject("PlaySuperStoreCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var buttonObject = new GameObject("StoreButton", typeof(RectTransform));
        buttonObject.transform.SetParent(canvasObject.transform, false);

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
        // A runtime canvas is inert without one -- clicks silently do nothing.
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
