using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Temporary store entry point for the menu screen, built entirely at runtime so no .unity or
/// .prefab file is touched. One way in, on the boot scene only.
///
/// This placeholder exists until an art-directed, positioned touchpoint replaces it - at which
/// point this file should be deleted and its audio handling moved into the touchpoint renderer.
/// </summary>
public class PlaySuperStoreEntry : MonoBehaviour
{
    private const string MenuSceneName = "menu";
    private const string RootObjectName = "PlaySuperStoreEntry";

    private static PlaySuperStoreEntry current;

    private float volumeBeforeStore = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            Spawn();
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            Spawn();
        }
    }

    private static void Spawn()
    {
        if (current != null)
        {
            return;
        }

        // A uGUI canvas needs an EventSystem in the scene or clicks silently do nothing.
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystem.transform.SetAsLastSibling();
        }

        GameObject root = new GameObject(RootObjectName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject buttonObject = new GameObject("StoreButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(420f, 130f);
        rect.anchoredPosition = new Vector2(-48f, 48f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0.09f, 0.11f, 0.18f, 0.94f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "REWARDS";
        label.fontSize = 44f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        current = root.AddComponent<PlaySuperStoreEntry>();
        buttonObject.GetComponent<Button>().onClick.AddListener(current.OpenStore);
    }

    private void OpenStore()
    {
        // The in-app GPM WebView. Never Application.OpenURL - OpenStore syncs analytics and
        // carries the player's session.
        if (PlaySuperUnitySDK.Instance == null)
        {
            Debug.LogWarning("[PlaySuper] SDK instance is null - cannot open the store.");
            return;
        }

        PlaySuperUnitySDK.Instance.OpenStore();
    }

    // The store events are STATIC members of the type. Writing them through .Instance is
    // compile error CS0176.
    private void OnEnable()
    {
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private void OnDisable()
    {
        // Static events outlive the component and would keep this object alive otherwise.
        PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            current = null;
        }
    }

    private void HandleStoreOpened()
    {
        // VolumeSpawn drives AudioListener.volume from the saved GameData, so duck through the
        // same channel and restore the exact value on close.
        volumeBeforeStore = AudioListener.volume;
        AudioListener.volume = 0f;
        AudioListener.pause = true;
    }

    private void HandleStoreClosed()
    {
        AudioListener.pause = false;
        AudioListener.volume = volumeBeforeStore;
    }
}
