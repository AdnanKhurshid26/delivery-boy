using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// TEMPORARY store entry point for the menu screen.
///
/// The menu scene has no existing shop button and no menu controller script, so this
/// builds a plain uGUI button entirely at runtime - no .unity or .prefab file is touched.
/// It auto-spawns via [RuntimeInitializeOnLoadMethod], gated to the boot scene.
///
/// This is deliberately a plain way in. The positioned, art-directed entry arrives later
/// as a PlaySuper touchpoint, and that touchpoint's renderer should DELETE this script in
/// the same change - two store entrances on one screen is not something anyone asked for.
/// </summary>
public class PlaySuperStoreEntry : MonoBehaviour
{
    // Boot scene, first entry in EditorBuildSettings.
    private const string MenuSceneName = "menu";
    private const string HostObjectName = "PlaySuperStoreEntry";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // Re-spawn whenever the player comes back to the menu from gameplay, not just on
        // the very first load.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
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

        if (GameObject.Find(HostObjectName) != null)
        {
            return;
        }

        new GameObject(HostObjectName).AddComponent<PlaySuperStoreEntry>();
    }

    private void Awake()
    {
        EnsureEventSystem();
        BuildButton();
    }

    private void OnEnable()
    {
        // OnStoreClosed is STATIC - qualified by the TYPE, not by .Instance. Static events
        // outlive the component, so the same handler is removed in OnDisable.
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private void OnDisable()
    {
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
    }

    private void OpenStore()
    {
        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            Debug.LogWarning("[PlaySuper] SDK instance not ready - store not opened.");
            return;
        }

        // Duck audio here rather than in an OnStoreOpened handler: the published docs only
        // document OnStoreClosed, and this is already the moment the store opens.
        // AudioListener.pause is orthogonal to AudioListener.volume, so the player's own
        // mute setting (applied by VolumeSpawn) survives untouched.
        AudioListener.pause = true;

        // In-app WebView, never Application.OpenURL - OpenStore syncs analytics.
        sdk.OpenStore();
    }

    private void HandleStoreClosed()
    {
        AudioListener.pause = false;
    }

    private void BuildButton()
    {
        GameObject canvasGo = new GameObject(
            "PlaySuperStoreCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject buttonGo = new GameObject("StoreButton", typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(300f, 100f);
        rect.anchoredPosition = new Vector2(-40f, -40f);

        buttonGo.GetComponent<Image>().color = new Color(0.09f, 0.55f, 0.95f, 0.95f);
        buttonGo.GetComponent<Button>().onClick.AddListener(OpenStore);

        GameObject labelGo = new GameObject("Label", typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(buttonGo.transform, false);

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 44f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private void EnsureEventSystem()
    {
        // A runtime uGUI canvas needs an EventSystem in the scene or clicks silently do
        // nothing.
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        go.transform.SetParent(transform, false);
    }
}
