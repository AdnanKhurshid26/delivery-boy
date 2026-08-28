using System;
using PlaySuperUnity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The game's single store entry point, on the menu (boot) scene only.
    ///
    /// Built entirely at runtime so no .unity or .prefab file had to be edited. This is a
    /// plain temporary way in — the positioned, art-directed entry point arrives later as
    /// a PlaySuper touchpoint and replaces this button.
    /// </summary>
    public class PlaySuperStoreEntryPoint : MonoBehaviour
    {
        private const string MenuSceneName = "menu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
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
            if (!scene.IsValid() || scene.name != MenuSceneName)
            {
                return;
            }

            if (FindObjectOfType<PlaySuperStoreEntryPoint>() != null)
            {
                return;
            }

            GameObject host = new GameObject("PlaySuperStoreEntryPoint");
            host.AddComponent<PlaySuperStoreEntryPoint>();
        }

        private void Awake()
        {
            EnsureEventSystem();
            BuildButton();
        }

        // The store events are STATIC — qualified by the type, not by .Instance
        // (PlaySuperUnitySDK.Instance.OnStoreOpened would be CS0176). They outlive this
        // component, so every handler added here is removed in OnDisable.
        private void OnEnable()
        {
            PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
        }

        private void OnDisable()
        {
            PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
        }

        private void HandleStoreOpened()
        {
            // Pause rather than zeroing volume, so the saved mixer level that VolumeSpawn
            // restores from GameData is left exactly as the player set it.
            AudioListener.pause = true;
        }

        private void HandleStoreClosed()
        {
            AudioListener.pause = false;
        }

        /// <summary>A uGUI canvas needs an EventSystem or clicks silently do nothing.</summary>
        private static void EnsureEventSystem()
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
            GameObject canvasObject = new GameObject("PlaySuperCanvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject buttonObject = new GameObject("PlaySuperStoreButton");
            buttonObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(300f, 110f);
            rect.anchoredPosition = new Vector2(-40f, -40f);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.65f, 0.42f, 1f);

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

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = "REWARDS";
            label.fontSize = 44f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
        }

        private void OpenStore()
        {
            try
            {
                if (PlaySuperUnitySDK.Instance == null)
                {
                    Debug.LogWarning("[PlaySuper] SDK not ready; cannot open the store.");
                    return;
                }

                // In-app WebView. Never Application.OpenURL — that skips analytics sync
                // and drops the player out of the game.
                PlaySuperUnitySDK.Instance.OpenStore();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
            }
        }
    }
}
