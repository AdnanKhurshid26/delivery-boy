using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlaySuperUnity;

/// <summary>
/// Spawns a "STORE" entry point on the main menu at runtime and opens the
/// PlaySuper in-app store when tapped. Built entirely in code (no scene/prefab
/// edits) on its own ScreenSpaceOverlay canvas.
///
/// This is the temporary setup-time entry point — a positioned store widget can
/// later replace it as a PlaySuper touchpoint.
/// </summary>
public static class PlaySuperStoreButton
{
    private const string MenuSceneName = "menu";
    private const string RootName = "PlaySuperStoreButton";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        // The boot (menu) scene is already loaded by the time this callback
        // subscribes, so handle it directly.
        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            SpawnButton();
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            SpawnButton();
        }
    }

    private static void SpawnButton()
    {
        // Avoid duplicates if the menu scene is loaded more than once.
        if (GameObject.Find(RootName) != null)
        {
            return;
        }

        EnsureEventSystem();

        var canvasGo = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // draw above the menu UI
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);

        var buttonGo = new GameObject("Button", typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(canvasGo.transform, false);

        var image = buttonGo.GetComponent<Image>();
        image.color = new Color(0.15f, 0.55f, 0.95f, 1f);

        var rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f); // bottom-right inset
        rect.sizeDelta = new Vector2(280f, 110f);

        var labelGo = new GameObject("Label", typeof(Text));
        labelGo.transform.SetParent(buttonGo.transform, false);
        var label = labelGo.GetComponent<Text>();
        label.text = "STORE";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontStyle = FontStyle.Bold;
        label.fontSize = 44;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        buttonGo.GetComponent<Button>().onClick.AddListener(OpenStore);
    }

    private static void OpenStore()
    {
        var sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            return;
        }
        sdk.OpenStore();
    }

    private static void EnsureEventSystem()
    {
        // A uGUI canvas needs an EventSystem or clicks are silently ignored.
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
