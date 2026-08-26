using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlaySuperUnity;

/// <summary>
/// The game's entry point into the PlaySuper store.
///
/// Delivery Boy has no menu UI controller script (the menu scene wires its buttons
/// directly), and scene/prefab files are off limits, so this builds its own button at
/// runtime on the menu scene only. Pressing Play is the only manual step.
/// </summary>
public class PlaySuperStoreButton : MonoBehaviour
{
    // Boot scene, first entry in EditorBuildSettings.
    private const string MenuSceneName = "menu";

    private static PlaySuperStoreButton instance;

    private bool audioPausedByUs;
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // The menu scene is already loaded when this runs at startup, so sceneLoaded
        // will not fire for it.
        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            Spawn();
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            Spawn();
        }
    }

    private static void Spawn()
    {
        // Scene-local by design: the button is destroyed on the way into a level, and
        // Unity reports the destroyed reference as null, so this rebuilds on return.
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("PlaySuperStore");
        instance = host.AddComponent<PlaySuperStoreButton>();
    }

    private void Awake()
    {
        EnsureEventSystem();
        BuildButton();
        Subscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        // Never leave audio muted because the object went away mid-store.
        if (audioPausedByUs)
        {
            AudioListener.pause = false;
            audioPausedByUs = false;
        }
    }

    /// <summary>
    /// A uGUI canvas needs an EventSystem in the scene or clicks silently do nothing.
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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject buttonObject = new GameObject("StoreButton");
        buttonObject.transform.SetParent(canvasObject.transform, false);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.12f, 0.55f, 0.95f, 1f);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(320f, 110f);
        rect.anchoredPosition = new Vector2(-40f, -40f);

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

    /// <summary>
    /// Always the SDK's in-app WebView, never Application.OpenURL - OpenStore is what
    /// keeps the session and analytics attached to the visit.
    /// </summary>
    private void OpenStore()
    {
        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK unavailable, cannot open the store.");
                return;
            }

            sdk.OpenStore();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
        }
    }

    private void Subscribe()
    {
        if (subscribed)
        {
            return;
        }

        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                return;
            }

            sdk.OnStoreOpened += HandleStoreOpened;
            sdk.OnStoreClosed += HandleStoreClosed;
            subscribed = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Could not subscribe to store events: " + e.Message);
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk != null)
            {
                sdk.OnStoreOpened -= HandleStoreOpened;
                sdk.OnStoreClosed -= HandleStoreClosed;
            }
        }
        catch (Exception)
        {
            // Shutting down; nothing useful to do here.
        }

        subscribed = false;
    }

    private void HandleStoreOpened()
    {
        // Only take responsibility for audio the game had running, so closing the
        // store cannot un-mute a player who muted the game themselves.
        if (!AudioListener.pause)
        {
            AudioListener.pause = true;
            audioPausedByUs = true;
        }
    }

    private void HandleStoreClosed()
    {
        if (audioPausedByUs)
        {
            AudioListener.pause = false;
            audioPausedByUs = false;
        }
    }
}
