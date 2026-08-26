using System;
using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Spawns the store entry point on the main menu.
///
/// Built at runtime rather than wired onto one of the existing menu buttons,
/// because that would mean editing menu.unity. The positioned, art-directed
/// store entry arrives later as a PlaySuper touchpoint; this is the plain
/// always-available way in.
/// </summary>
public class PlaySuperStore : MonoBehaviour
{
    private const string MenuSceneName = "menu";

    private static PlaySuperStore _instance;

    private float _volumeBeforeStore = 1f;
    private bool _subscribed;

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

        GameObject host = new GameObject("PlaySuperStore");
        _instance = host.AddComponent<PlaySuperStore>();
    }

    private void Awake()
    {
        // Scene-local: a fresh one spawns each time the menu loads.
        EnsureEventSystem();
        BuildButton();
        SubscribeStoreEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeStoreEvents();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// A uGUI Canvas without an EventSystem swallows clicks silently, so never
    /// assume the scene already has one.
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

    private void BuildButton()
    {
        GameObject canvasObject = new GameObject("PlaySuperStoreCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject buttonObject = new GameObject("RewardsButton");
        buttonObject.transform.SetParent(canvasObject.transform, false);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.55f, 0.95f, 1f);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(300f, 110f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(OpenStore);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.fontSize = 40f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void OpenStore()
    {
        try
        {
            if (PlaySuperUnitySDK.Instance == null)
            {
                return;
            }

            // In-app WebView. Never Application.OpenURL -- OpenStore syncs analytics.
            PlaySuperUnitySDK.Instance.OpenStore();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
        }
    }

    // The store lifecycle events are the one SDK surface that could not be
    // verified against this repo. If these two methods fail to compile, the
    // damage is contained here: the store still opens without the audio pause.
    private void SubscribeStoreEvents()
    {
        try
        {
            if (PlaySuperUnitySDK.Instance == null || _subscribed)
            {
                return;
            }

            PlaySuperUnitySDK.Instance.OnStoreOpened += HandleStoreOpened;
            PlaySuperUnitySDK.Instance.OnStoreClosed += HandleStoreClosed;
            _subscribed = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Could not subscribe to store events: " + e.Message);
        }
    }

    private void UnsubscribeStoreEvents()
    {
        try
        {
            if (PlaySuperUnitySDK.Instance == null || !_subscribed)
            {
                return;
            }

            PlaySuperUnitySDK.Instance.OnStoreOpened -= HandleStoreOpened;
            PlaySuperUnitySDK.Instance.OnStoreClosed -= HandleStoreClosed;
            _subscribed = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Could not unsubscribe from store events: " + e.Message);
        }
    }

    private void HandleStoreOpened()
    {
        _volumeBeforeStore = AudioListener.volume;
        AudioListener.volume = 0f;
    }

    private void HandleStoreClosed()
    {
        // Restore game state on close rather than assuming the menu's own volume
        // handling will run -- VolumeManager only applies its value on load.
        AudioListener.volume = _volumeBeforeStore;
    }
}
