using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlaySuperUnity;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The game's single store entry point, on the boot/menu scene.
    ///
    /// TEMPORARY BY DESIGN. This is a plain, unstyled way in so the store is reachable
    /// while the core loop is verified. The positioned, art-directed entry point arrives
    /// later as a PlaySuper touchpoint and REPLACES this file - when that lands, delete
    /// this script and move the audio pause/resume handling below into the renderer.
    ///
    /// Built entirely at runtime: menu.unity is not edited, so there is nothing to merge
    /// in a scene file and nothing to undo when the touchpoint takes over.
    /// </summary>
    public class PlaySuperStoreButton : MonoBehaviour
    {
        // First entry in EditorBuildSettings, i.e. the boot scene.
        private const string MenuSceneName = "menu";

        private bool audioWasPausedByStore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
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
            if (sceneName != MenuSceneName)
            {
                return;
            }

            if (FindObjectOfType<PlaySuperStoreButton>() != null)
            {
                return;
            }

            new GameObject("PlaySuperStoreButton").AddComponent<PlaySuperStoreButton>();
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildButton();
        }

        private void OnEnable()
        {
            // These events are STATIC - they hang off the type, not the instance.
            PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
        }

        private void OnDisable()
        {
            // Static events outlive this component and would keep a dead object alive.
            PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
        }

        /// <summary>
        /// A uGUI canvas needs an EventSystem in the scene or clicks silently do nothing.
        /// menu.unity may not have one, and we cannot edit the scene to add it.
        /// </summary>
        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject es = new GameObject("PlaySuperEventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private void BuildButton()
        {
            GameObject canvasObject = new GameObject("PlaySuperStoreCanvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the menu's own UI without disturbing it.
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject buttonObject = new GameObject("RewardsButton");
            buttonObject.transform.SetParent(canvasObject.transform, false);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.13f, 0.13f, 0.16f, 0.92f);

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            // Top-right, clear of the menu's centre content.
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(300f, 110f);
            rect.anchoredPosition = new Vector2(-40f, -40f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(OpenStore);

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);

            Text label = labelObject.AddComponent<Text>();
            label.text = "REWARDS";
            label.font = LoadBuiltinFont();
            label.fontSize = 40;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Built-in uGUI font rather than the menu's TMP asset, so this script carries no
        /// serialised asset reference and stays deletable in one move. The resource was
        /// renamed in Unity 2022.2, hence the fallback.
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

        private void OpenStore()
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK not initialised - cannot open the store.");
                return;
            }

            // In-app WebView. Never Application.OpenURL: that skips the WebView and loses
            // the analytics sync.
            sdk.OpenStore();
        }

        private void HandleStoreOpened()
        {
            // Duck the menu music while the store's own content is in front of the player.
            if (!AudioListener.pause)
            {
                AudioListener.pause = true;
                audioWasPausedByStore = true;
            }
        }

        private void HandleStoreClosed()
        {
            // Only undo what we did - the game may have paused audio itself.
            if (audioWasPausedByStore)
            {
                AudioListener.pause = false;
                audioWasPausedByStore = false;
            }
        }
    }
}
