using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using PlaySuperUnity;

/// <summary>
/// Store entry point for the menu screen.
///
/// menu.unity has no menu-controller script to hang a button off, and scene/prefab
/// files are off limits, so the button is built at runtime on its own overlay canvas.
/// It only appears in the menu scene; the positioned in-game store widget arrives
/// later as a PlaySuper touchpoint.
/// </summary>
public class PlaySuperStoreButton : MonoBehaviour
{
    private const string MenuSceneName = "menu";

    private static bool _hooked;
    private static float _timeScaleBeforeStore = 1f;
    private static bool _audioPausedBeforeStore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        if (_hooked) return;
        _hooked = true;

        SceneManager.sceneLoaded += (scene, mode) => TrySpawn(scene.name);
        TrySpawn(SceneManager.GetActiveScene().name);

        try
        {
            // Store-close handling: restore whatever the game was doing.
            PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Could not subscribe to store events: " + e.Message);
        }
    }

    private static void TrySpawn(string sceneName)
    {
        if (sceneName != MenuSceneName) return;
        if (!PlaySuperBootstrap.IsReady) return;
        if (GameObject.Find("PlaySuperStoreCanvas") != null) return;

        Build();
    }

    private static void Build()
    {
        // A uGUI canvas needs an EventSystem or clicks silently do nothing, and this
        // project may not have one in the menu scene.
#if UNITY_2023_1_OR_NEWER
        bool hasEventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null;
#else
        bool hasEventSystem = UnityEngine.Object.FindObjectOfType<EventSystem>() != null;
#endif
        if (!hasEventSystem)
        {
            new GameObject("PlaySuperEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        var canvasGo = new GameObject("PlaySuperStoreCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        var buttonGo = new GameObject("PlaySuperStoreButton", typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(canvasGo.transform, false);

        var rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(320f, 110f);

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.13f, 0.15f, 0.22f, 0.94f);

        var labelGo = new GameObject("Label", typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(buttonGo.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.fontSize = 44f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        buttonGo.GetComponent<Button>().onClick.AddListener(OpenStore);
    }

    private static void OpenStore()
    {
        try
        {
            // In-app WebView. Never Application.OpenURL — that loses analytics sync.
            PlaySuperUnitySDK.Instance.OpenStore();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
        }
    }

    private static void HandleStoreOpened()
    {
        _timeScaleBeforeStore = Time.timeScale;
        _audioPausedBeforeStore = AudioListener.pause;

        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    private static void HandleStoreClosed()
    {
        Time.timeScale = _timeScaleBeforeStore <= 0f ? 1f : _timeScaleBeforeStore;
        AudioListener.pause = _audioPausedBeforeStore;
    }
}
