using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlaySuperUnity;

/// <summary>
/// Runtime-built store entry point for the main menu.
///
/// The menu scene has no existing shop button to attach OpenStore to, and scene
/// and prefab files must not be edited, so the button is constructed in code on
/// its own overlay canvas. Scene-gated to the menu so it never appears during
/// gameplay. A positioned, art-directed store widget can replace this later as
/// a PlaySuper touchpoint.
/// </summary>
public class PlaySuperStoreButton : MonoBehaviour
{
    private const string MenuSceneName = "menu";
    private const string RootObjectName = "PlaySuperStoreButton";

    private static bool eventsHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (scene, mode) => TrySpawn(scene.name);

        // AfterSceneLoad fires once the first scene is already up, so sceneLoaded
        // will not report it. Handle it directly.
        TrySpawn(SceneManager.GetActiveScene().name);

        HookStoreEvents();
    }

    private static void HookStoreEvents()
    {
        if (eventsHooked)
        {
            return;
        }

        try
        {
            PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
            eventsHooked = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Could not hook store events: " + e.Message);
        }
    }

    private static void HandleStoreOpened()
    {
        // The store takes over the screen via the in-app WebView; silence the
        // menu music while it is up.
        AudioListener.pause = true;
    }

    private static void HandleStoreClosed()
    {
        AudioListener.pause = false;
    }

    private static void TrySpawn(string sceneName)
    {
        if (sceneName != MenuSceneName)
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
            // A missing store button must never take the menu down with it.
            Debug.LogWarning("[PlaySuper] Store button build failed: " + e.Message);
        }
    }

    private static void Build()
    {
        EnsureEventSystem();

        GameObject root = new GameObject(RootObjectName);
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        GameObject buttonObject = new GameObject("RewardsButton");
        buttonObject.transform.SetParent(root.transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(300f, 110f);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.13f, 0.13f, 0.16f, 0.92f);

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

        Text label = labelObject.AddComponent<Text>();
        label.text = "REWARDS";
        label.font = LoadBuiltinFont();
        label.fontSize = 40;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;

        UnityEngine.Object.DontDestroyOnLoad(root);
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (root != null)
            {
                root.SetActive(scene.name == MenuSceneName);
            }
        };
    }

    private static Font LoadBuiltinFont()
    {
        // Unity 2022+ ships LegacyRuntime.ttf; older editors only have Arial.
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return font;
    }

    private static void EnsureEventSystem()
    {
        // Without an EventSystem in the scene, a uGUI button receives no clicks
        // and fails completely silently.
#if UNITY_2023_1_OR_NEWER
        EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
        EventSystem existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
        if (existing != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("PlaySuperEventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
        UnityEngine.Object.DontDestroyOnLoad(eventSystem);
    }

    private static void OpenStore()
    {
        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                return;
            }

            // Always the SDK's in-app WebView, never Application.OpenURL —
            // OpenStore syncs analytics and session state.
            sdk.OpenStore();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
        }
    }
}
