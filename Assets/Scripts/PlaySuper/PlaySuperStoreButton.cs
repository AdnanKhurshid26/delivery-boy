using System;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Spawns the rewards-store entry point on the menu scene at runtime. Built in code
/// rather than in the scene so no .unity or .prefab file has to be edited; pressing
/// Play is the only step needed to see it.
/// </summary>
public class PlaySuperStoreButton : MonoBehaviour
{
    private const string MenuSceneName = "menu";
    private const string RootObjectName = "PlaySuperStoreButton";

    private float volumeBeforeStore = 1f;
    private bool isStoreOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        // Fires for later loads (returning to the menu from a level)...
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // ...and this covers the very first scene, which loaded before this hook ran.
        TrySpawn(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawn(scene);
    }

    private static void TrySpawn(Scene scene)
    {
        if (!scene.IsValid() || scene.name != MenuSceneName)
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
            // A missing store button must never break the menu.
            Debug.LogWarning("[PlaySuper] Could not build the store button: " + e.Message);
        }
    }

    private static void Build()
    {
        EnsureEventSystem();

        var root = new GameObject(RootObjectName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var buttonObject = new GameObject("RewardsButton", typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root.transform, false);

        var buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-40f, -40f);
        buttonRect.sizeDelta = new Vector2(320f, 110f);

        var background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.12f, 0.12f, 0.16f, 0.92f);

        var labelObject = new GameObject("Label", typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);

        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<Text>();
        label.text = "REWARDS";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 44;
        label.fontStyle = FontStyle.Bold;
        label.font = LoadBuiltinFont();

        var handler = root.AddComponent<PlaySuperStoreButton>();
        buttonObject.GetComponent<Button>().onClick.AddListener(handler.OpenStore);
    }

    /// <summary>
    /// Built-in font name changed across Unity versions; try both before giving up.
    /// </summary>
    private static Font LoadBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    /// <summary>
    /// Without an EventSystem in the scene, uGUI clicks are silently swallowed.
    /// </summary>
    private static void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        bool hasEventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null;
#else
        bool hasEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>() != null;
#endif

        if (hasEventSystem)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void OpenStore()
    {
        if (isStoreOpen)
        {
            return;
        }

        try
        {
            if (PlaySuperUnitySDK.Instance == null)
            {
                Debug.LogWarning("[PlaySuper] Store unavailable: SDK not initialized.");
                return;
            }

            // Capture the current volume rather than assuming full - a muted player
            // must stay muted when the store closes.
            volumeBeforeStore = AudioListener.volume;
            AudioListener.volume = 0f;
            isStoreOpen = true;

            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;

            // Always the SDK's in-app WebView, never Application.OpenURL.
            PlaySuperUnitySDK.Instance.OpenStore();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
            HandleStoreClosed();
        }
    }

    private void HandleStoreClosed()
    {
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
        AudioListener.volume = volumeBeforeStore;
        isStoreOpen = false;
    }

    private void OnDestroy()
    {
        if (isStoreOpen)
        {
            PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
            AudioListener.volume = volumeBeforeStore;
            isStoreOpen = false;
        }
    }
}
