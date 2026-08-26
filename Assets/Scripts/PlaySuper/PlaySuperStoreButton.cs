using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlaySuperUnity;

/// <summary>
/// Store entry point for the menu screen, built entirely at runtime so no scene or prefab
/// is modified. A positioned, art-directed store entry arrives later as a PlaySuper
/// touchpoint - this is just the plain door into the store for core setup.
/// </summary>
public static class PlaySuperStoreButton
{
    private const string MenuSceneName = "menu";
    private const string RootObjectName = "PlaySuperStoreButtonCanvas";

    private static bool isHooked;
    private static bool audioPausedByStore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        if (isHooked)
        {
            return;
        }

        isHooked = true;

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Store-close handling: restore game state when the in-app WebView goes away.
        // NOTE: if your SDK version exposes these on the instance rather than statically,
        // move these four lines onto PlaySuperUnitySDK.Instance.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;

        TrySpawn(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawn(scene);
    }

    private static void TrySpawn(Scene scene)
    {
        if (scene.name != MenuSceneName)
        {
            return;
        }

        if (GameObject.Find(RootObjectName) != null)
        {
            return;
        }

        try
        {
            Build();
        }
        catch (Exception e)
        {
            // A rewards button must never break the screen it sits on: fail silent-and-absent.
            Debug.LogWarning("[PlaySuper] Store button could not be built: " + e.Message);
        }
    }

    private static void Build()
    {
        // A uGUI canvas does nothing without an EventSystem - clicks are silently swallowed.
        // The menu scene has its own, but never assume it.
#if UNITY_2023_1_OR_NEWER
        bool hasEventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null;
#else
        bool hasEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>() != null;
#endif
        if (!hasEventSystem)
        {
            GameObject eventSystem = new GameObject("PlaySuperEventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        GameObject root = new GameObject(RootObjectName);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        GameObject buttonObject = new GameObject("StoreButton");
        buttonObject.transform.SetParent(root.transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        rect.sizeDelta = new Vector2(300f, 110f);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.11f, 0.09f, 0.24f, 0.95f);

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
        label.fontSize = 42f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
    }

    private static void OpenStore()
    {
        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK not ready; cannot open store.");
                return;
            }

            // Always the SDK's in-app WebView - never Application.OpenURL, which would skip
            // analytics and drop the player out of the game.
            sdk.OpenStore();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
        }
    }

    private static void HandleStoreOpened()
    {
        if (AudioListener.pause)
        {
            // Already muted by the game's own settings - leave it alone so closing the
            // store does not unmute a player who chose silence.
            return;
        }

        audioPausedByStore = true;
        AudioListener.pause = true;
    }

    private static void HandleStoreClosed()
    {
        if (!audioPausedByStore)
        {
            return;
        }

        audioPausedByStore = false;
        AudioListener.pause = false;
    }
}
