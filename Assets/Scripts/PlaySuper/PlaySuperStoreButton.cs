using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The store entry point for the menu screen.
///
/// Scenes and prefabs are never edited, so the canvas, the button and (when the scene
/// lacks one) an EventSystem are all built at runtime. Auto-spawns on the menu scene only.
/// A positioned, art-directed store widget can replace this later as a touchpoint.
/// </summary>
public class PlaySuperStoreButton : MonoBehaviour
{
    private const string MenuSceneName = "menu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySpawn(SceneManager.GetActiveScene().name);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawn(scene.name);
    }

    private static void TrySpawn(string sceneName)
    {
        if (sceneName != MenuSceneName) return;
        if (FindObjectOfType<PlaySuperStoreButton>() != null) return;

        new GameObject("PlaySuperStore").AddComponent<PlaySuperStoreButton>();
    }

    private void Awake()
    {
        EnsureEventSystem();
        BuildButton();
    }

    private void OnEnable()
    {
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private void OnDisable()
    {
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
    }

    /// <summary>
    /// A uGUI canvas with no EventSystem in the scene swallows every click silently,
    /// so never assume the game already has one.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private void BuildButton()
    {
        GameObject canvasGo = new GameObject("PlaySuperStoreCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject buttonGo = new GameObject("RewardsButton");
        buttonGo.transform.SetParent(canvasGo.transform, false);

        Image background = buttonGo.AddComponent<Image>();
        background.color = new Color(0.10f, 0.11f, 0.16f, 0.94f);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        rect.sizeDelta = new Vector2(320f, 120f);

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(OpenStore);

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(buttonGo.transform, false);

        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.fontSize = 48f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void OpenStore()
    {
        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null) return;

        // Audio is paused here rather than on an open event so the two calls stay adjacent.
        AudioListener.pause = true;

        // Always the SDK's in-app WebView, never Application.OpenURL - OpenStore carries the
        // auth token and syncs purchase analytics back into the game.
        sdk.OpenStore(null, "main_menu_button");
    }

    private void HandleStoreClosed()
    {
        AudioListener.pause = false;
    }
}
