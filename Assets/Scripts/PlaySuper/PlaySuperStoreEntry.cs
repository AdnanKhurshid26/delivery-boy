using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The game's single PlaySuper store entry point, on the menu (boot) scene.
///
/// Built entirely at runtime in code - menu.unity is never edited. This is a
/// plain temporary way in; the positioned, art-directed entry arrives later as a
/// touchpoint and replaces it.
/// </summary>
public class PlaySuperStoreEntry : MonoBehaviour
{
    private const string MenuSceneName = "menu";

    private GameObject canvasRoot;
    private float timeScaleBeforeStore = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        GameObject host = new GameObject("PlaySuperStoreEntry");
        DontDestroyOnLoad(host);
        host.AddComponent<PlaySuperStoreEntry>();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // These events are STATIC - qualified by the type, never by .Instance.
        // Static events outlive this component, so they are unsubscribed in
        // OnDisable or they keep a dead object alive.
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
            BuildButton();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            BuildButton();
        }
        else
        {
            DestroyButton();
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

    private void OpenStore()
    {
        // Always the SDK's in-app WebView, never Application.OpenURL - OpenStore
        // carries the session and syncs analytics.
        if (PlaySuperUnitySDK.Instance == null)
        {
            Debug.LogWarning("[PlaySuper] Store tapped before the SDK was ready.");
            return;
        }

        PlaySuperUnitySDK.Instance.OpenStore();
    }

    private void BuildButton()
    {
        if (canvasRoot != null)
        {
            return;
        }

        EnsureEventSystem();

        canvasRoot = new GameObject("PlaySuperStoreCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        GameObject buttonObject = new GameObject("StoreButton");
        buttonObject.transform.SetParent(canvasRoot.transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(320f, 110f);
        rect.anchoredPosition = new Vector2(-40f, -40f);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.55f, 0.95f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(OpenStore);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.fontSize = 40f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private void DestroyButton()
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
}
