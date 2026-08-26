using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using PlaySuperUnity;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// Runtime store entry point for the menu screen.
    ///
    /// The menu scene has no shop button to hang OpenStore on, and scene/prefab files must
    /// not be edited, so the button is built in code and spawned only in the menu scene.
    /// This is a functional placeholder: the properly designed, positioned store entry
    /// arrives later as a PlaySuper touchpoint and can replace this.
    /// </summary>
    public class PlaySuperStoreEntry : MonoBehaviour
    {
        private const string MenuSceneName = "menu";
        private const string ButtonLabel = "REWARDS";

        private static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (hooked)
            {
                return;
            }

            hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SpawnIfMenu(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SpawnIfMenu(scene);
        }

        private static void SpawnIfMenu(Scene scene)
        {
            if (scene.name != MenuSceneName)
            {
                return;
            }

            GameObject host = new GameObject("PlaySuperStoreEntry");
            host.AddComponent<PlaySuperStoreEntry>();
        }

        private void Start()
        {
            EnsureEventSystem();
            BuildButton();
            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
        }

        private void OnDestroy()
        {
            PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
        }

        /// <summary>
        /// A uGUI canvas needs an EventSystem in the scene or clicks silently do nothing.
        /// Never assume the game has one.
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
            GameObject canvasObject = new GameObject("PlaySuperStoreCanvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject buttonObject = new GameObject("StoreButton");
            buttonObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(300f, 110f);
            rect.anchoredPosition = new Vector2(-40f, -40f);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.55f, 0.25f, 0.95f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(OnStoreButtonClick);

            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = ButtonLabel;
            label.fontSize = 44f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private void OnStoreButtonClick()
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                return;
            }

            // Pause audio while the in-app store WebView is in front of the game.
            // AudioListener.pause is used rather than AudioListener.volume so the player's
            // saved mute/volume settings in GameData are left untouched.
            AudioListener.pause = true;

            // Must be the SDK's in-app WebView, never Application.OpenURL -- OpenStore is
            // what syncs analytics and the player's session.
            sdk.OpenStore();
        }

        private void HandleStoreClosed()
        {
            AudioListener.pause = false;
        }
    }
}
