using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// Shared renderer for this game's PlaySuper touchpoints.
    ///
    /// The SDK renders nothing — the game composites the widget from the node's fields.
    /// Both of this game's touchpoints are the "home-entry-widget" pattern: a single leaf
    /// node carrying one image and a CTA action, with the whole widget as the tap target.
    /// Their nodes carry NO title, subtitle, badge, cta.text, cta.backgroundImage or
    /// background, so there is deliberately no text-drawing or CTA-compositing code here.
    ///
    /// Everything is built at runtime in C# — no .unity or .prefab file is touched.
    /// </summary>
    public abstract class PlaySuperTouchpointWidget : MonoBehaviour
    {
        /// <summary>Lowercase-kebab touchpoint name; also the utm_content value.</summary>
        protected abstract string TouchpointName { get; }

        /// <summary>
        /// The accepted placement, normalized 0-1 with a TOP-LEFT origin — x, y, w, h
        /// straight from the suggestion, converted to uGUI anchors below.
        /// </summary>
        protected abstract Rect Bbox { get; }

        private const int OverlaySortingOrder = 900;

        // The store events are STATIC Actions. Subscribing through .Instance would be
        // CS0176. They outlive this component, so every handler is removed in OnDisable.
        //
        // This audio ducking moved here from the core-setup placeholder store button,
        // which this touchpoint replaces on the menu screen.
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
            // Pause rather than zeroing volume, so the saved mixer level VolumeSpawn
            // restores from GameData stays exactly as the player set it.
            AudioListener.pause = true;
        }

        private void HandleStoreClosed()
        {
            AudioListener.pause = false;
        }

        private async void Start()
        {
            try
            {
                await BuildAsync();
            }
            catch (Exception e)
            {
                // Fail silent-and-absent: a touchpoint must never break the screen it is on.
                Debug.LogWarning(
                    "[PlaySuper] touchpoint '" + TouchpointName + "' failed: " + e.Message);
            }
        }

        private async Task BuildAsync()
        {
            // Live fetch. The hydrated JSON is never embedded in the game — re-arting or
            // re-positioning this touchpoint in Studio needs no code change.
            TouchpointResponse touchpoint = await TouchpointManager.GetTouchpointByName(
                TouchpointName, PlaySuperBootstrap.CoinId);

            // All TouchpointManager calls return null on ANY error; they never throw.
            if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
            {
                return;
            }

            TouchpointNode node = touchpoint.nodes[0];
            if (node == null || node.images == null || node.images.Length == 0
                || string.IsNullOrEmpty(node.images[0]))
            {
                return;
            }

            Texture2D art = await Download(node.images[0]);
            if (art == null)
            {
                return;
            }

            // The player may have left the screen while the art downloaded.
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            string action = node.cta != null ? node.cta.action : null;
            BuildWidget(art, action);
        }

        private void BuildWidget(Texture2D art, string action)
        {
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("PlaySuperTouchpointCanvas_" + TouchpointName);
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            // Placement slot: bbox is top-left origin, uGUI anchors are bottom-left origin.
            GameObject slotObject = new GameObject("Slot");
            slotObject.transform.SetParent(canvasObject.transform, false);

            RectTransform slot = slotObject.AddComponent<RectTransform>();
            slot.anchorMin = new Vector2(Bbox.x, 1f - Bbox.y - Bbox.height);
            slot.anchorMax = new Vector2(Bbox.x + Bbox.width, 1f - Bbox.y);
            slot.offsetMin = Vector2.zero;
            slot.offsetMax = Vector2.zero;

            // The art lives in its own child of the slot, NOT on the slot itself:
            // AspectRatioFitter.FitInParent sizes against its PARENT and ignores the
            // sub-region anchors, so putting the fitter on the slot would fight the
            // placement instead of fitting inside it.
            GameObject artObject = new GameObject("Art");
            artObject.transform.SetParent(slotObject.transform, false);

            RectTransform artRect = artObject.AddComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.5f, 0.5f);
            artRect.anchorMax = new Vector2(0.5f, 0.5f);
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.anchoredPosition = Vector2.zero;

            RawImage image = artObject.AddComponent<RawImage>();
            image.texture = art;

            AspectRatioFitter fitter = artObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = art.height > 0 ? (float)art.width / art.height : 1f;

            // home-entry-widget: the whole widget is the tap target, no close control.
            Button button = artObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(delegate { OpenStore(action); });
        }

        private void OpenStore(string action)
        {
            try
            {
                if (PlaySuperUnitySDK.Instance == null)
                {
                    Debug.LogWarning("[PlaySuper] SDK not ready; cannot open the store.");
                    return;
                }

                if (string.IsNullOrEmpty(action))
                {
                    PlaySuperUnitySDK.Instance.OpenStore();
                    return;
                }

                // Passed through UNCHANGED — utm params are already appended server-side.
                // In-app WebView; never Application.OpenURL.
                PlaySuperUnitySDK.Instance.OpenStore(action);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] OpenStore failed: " + e.Message);
            }
        }

        /// <summary>A uGUI canvas needs an EventSystem or taps silently do nothing.</summary>
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

        private static async Task<Texture2D> Download(string url)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[PlaySuper] asset download failed: " + request.error);
                    return null;
                }

                return DownloadHandlerTexture.GetContent(request);
            }
        }
    }
}
