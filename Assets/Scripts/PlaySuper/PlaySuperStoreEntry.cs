using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime-built "REWARDS" button that opens the PlaySuper store from the main
/// menu.
///
/// Built entirely in code on a ScreenSpaceOverlay canvas -- no .unity or .prefab
/// file is edited, so this cannot conflict with scene work. It spawns itself only
/// on the menu scene and tears itself down when that scene unloads.
/// </summary>
public class PlaySuperStoreEntry : MonoBehaviour
{
    // Boot scene, per ProjectSettings/EditorBuildSettings.asset.
    private const string MenuSceneName = "menu";

    private static PlaySuperStoreEntry instance;

    private float volumeBeforeStore = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
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
        if (sceneName != MenuSceneName || instance != null)
        {
            return;
        }

        GameObject host = new GameObject("PlaySuperStoreEntry");
        instance = host.AddComponent<PlaySuperStoreEntry>();
    }

    private void Awake()
    {
        EnsureEventSystem();
        BuildButton();
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private void OnDestroy()
    {
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;

        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// A uGUI canvas needs an EventSystem in the scene or clicks silently do
    /// nothing -- never assume one exists.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private void BuildButton()
    {
        // --- Canvas -------------------------------------------------------
        GameObject canvasObject = new GameObject("PlaySuperCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above the menu UI

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        // Keep the button a sane physical size on high-DPI phones without
        // guessing a reference resolution the project does not declare.
        scaler.scaleFactor = Mathf.Max(1f, Screen.height / 1280f);

        canvasObject.AddComponent<GraphicRaycaster>();

        // --- Button -------------------------------------------------------
        GameObject buttonObject = new GameObject("RewardsButton");
        buttonObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(210f, 68f);
        rect.anchoredPosition = new Vector2(-24f, -24f);

        Image background = buttonObject.AddComponent<Image>();
        background.color = new Color(0.13f, 0.62f, 0.35f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(OpenStore);

        // --- Label --------------------------------------------------------
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.fontSize = 30f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private void OpenStore()
    {
        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            // SDK calls return null on error -- the button just does nothing
            // rather than breaking the menu.
            return;
        }

        // The menu's music keeps playing under the WebView otherwise.
        // VolumeManager drives AudioListener.volume, so mirror that here and
        // restore the exact value on close.
        volumeBeforeStore = AudioListener.volume;
        AudioListener.volume = 0f;

        // In-app GPM WebView. Never Application.OpenURL -- that would skip the
        // auth session and analytics sync.
        sdk.OpenStore();
    }

    private void HandleStoreClosed()
    {
        AudioListener.volume = volumeBeforeStore;
    }
}
