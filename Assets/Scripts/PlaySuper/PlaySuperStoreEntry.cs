using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using PlaySuperUnity;

/// <summary>
/// The game's single PlaySuper store entry point, on the boot/menu screen.
///
/// TEMPORARY BY DESIGN. This is a plain way into the store until the positioned, art-directed
/// entry arrives as a server-configured touchpoint - which will replace this button. That is
/// why its exact look and placement are not worth fussing over now.
///
/// Built entirely at runtime in C#: menu.unity has no existing shop button and scene files are
/// never edited by this integration.
/// </summary>
public class PlaySuperStoreEntry : MonoBehaviour
{
    // First entry in EditorBuildSettings, i.e. the boot/menu scene.
    private const string MenuSceneName = "menu";

    private bool audioWasPausedBeforeStore;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoSpawn()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TrySpawnFor(SceneManager.GetActiveScene().name);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySpawnFor(scene.name);
    }

    private static void TrySpawnFor(string sceneName)
    {
        if (sceneName != MenuSceneName)
        {
            return;
        }

        // Not DontDestroyOnLoad, so the previous instance is gone by now; this only guards
        // against a double spawn within one scene load.
        if (FindObjectOfType<PlaySuperStoreEntry>() != null)
        {
            return;
        }

        new GameObject("PlaySuperStoreEntry").AddComponent<PlaySuperStoreEntry>();
    }

    private void OnEnable()
    {
        // OnStoreOpened / OnStoreClosed are STATIC and must be qualified by the TYPE.
        // Writing PlaySuperUnitySDK.Instance.OnStoreOpened is error CS0176 and is easy to do by
        // accident, because the OpenStore call below does go through .Instance.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private void OnDisable()
    {
        // Static events outlive this component and would keep a dead object alive otherwise.
        PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
    }

    private void Start()
    {
        EnsureEventSystem();
        BuildButton();
    }

    /// <summary>
    /// A uGUI Canvas needs an EventSystem in the scene or clicks silently do nothing.
    /// </summary>
    private void EnsureEventSystem()
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

        GameObject buttonGo = new GameObject("StoreButton");
        buttonGo.transform.SetParent(canvasGo.transform, false);

        Image background = buttonGo.AddComponent<Image>();
        background.color = new Color(0.15f, 0.65f, 0.35f, 1f);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-40f, -40f);
        rect.sizeDelta = new Vector2(300f, 110f);

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(OpenStore);

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(buttonGo.transform, false);

        // TextMeshProUGUI with no explicit font falls back to TMP's default font asset, which the
        // project already ships (TMP is used throughout GamePlayManager).
        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.fontSize = 40f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        RectTransform labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
    }

    private void OpenStore()
    {
        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK instance is null; cannot open the store.");
                return;
            }

            // Must be OpenStore, never Application.OpenURL: OpenStore uses the SDK's in-app
            // WebView and syncs analytics.
            sdk.OpenStore();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
        }
    }

    private void HandleStoreOpened()
    {
        // The store is a full-screen native WebView over the menu, so duck the menu music
        // rather than leaving it playing underneath.
        audioWasPausedBeforeStore = AudioListener.pause;
        AudioListener.pause = true;
    }

    private void HandleStoreClosed()
    {
        // Restore rather than force-unpause, so a menu that was already muted stays muted.
        AudioListener.pause = audioWasPausedBeforeStore;
    }
}
