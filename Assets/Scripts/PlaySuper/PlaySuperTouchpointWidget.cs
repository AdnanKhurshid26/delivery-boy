using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;
using PlaySuperUnity;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// Base renderer for Delivery Boy's PlaySuper touchpoints.
    ///
    /// Both of this game's live touchpoints are the same shape - the
    /// "home-entry-widget" pattern: ONE leaf node carrying a single piece of pre-baked
    /// art plus a cta.action, no title, no subtitle, no badge, no separate CTA button
    /// art and no reward or product data. So the whole widget is the tap target and
    /// there is nothing to composite on top of the art. That is why this class draws
    /// exactly one image and nothing else; the two subclasses only supply the fetch
    /// name and the placement.
    ///
    /// The config is NEVER baked in: every mount fetches live from
    /// TouchpointManager.GetTouchpointByName, so the studio can swap the art or the
    /// destination in PlaySuper without another build.
    ///
    /// Everything is built at runtime on its own overlay canvas - no scene or prefab
    /// is touched - and every step fails silent-and-absent, because a touchpoint that
    /// cannot load must never break the screen it sits on.
    /// </summary>
    public abstract class PlaySuperTouchpointWidget : MonoBehaviour
    {
        /// <summary>The touchpoint's lowercase-kebab name: the fetch key and utm_content.</summary>
        protected abstract string TouchpointName { get; }

        /// <summary>
        /// The accepted placement, normalized 0-1 with a TOP-LEFT origin, exactly as the
        /// placement model recorded it against the screenshot of this screen.
        /// </summary>
        protected abstract Rect Placement { get; }

        /// <summary>Above the game's own UI. Overridable if a screen ever needs more room.</summary>
        protected virtual int SortingOrder => 900;

        private bool audioWasPausedByStore;
        private string ctaAction;

        private void OnEnable()
        {
            // STATIC events - they hang off the type, not the instance. Reaching them
            // through .Instance is a compile error (CS0176).
            PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
        }

        private void OnDisable()
        {
            // Static events outlive this component and would otherwise keep a dead
            // object reachable.
            PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;

            // If the level ends or the scene changes while the store is still open,
            // OnStoreClosed will never reach us - so undo the duck here rather than
            // leaving the game permanently silent.
            if (audioWasPausedByStore)
            {
                AudioListener.pause = false;
                audioWasPausedByStore = false;
            }
        }

        private async void Start()
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            // Every TouchpointManager call returns null on ANY error - they log and never
            // throw - so null is the expected failure path, not an exception.
            TouchpointResponse touchpoint =
                await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperRewards.CoinId);

            if (touchpoint?.nodes == null || touchpoint.nodes.Length == 0)
            {
                return;
            }

            TouchpointNode node = touchpoint.nodes[0];
            if (node?.images == null || node.images.Length == 0 || string.IsNullOrEmpty(node.images[0]))
            {
                return;
            }

            Texture2D art = await Download(node.images[0]);

            // Two awaits have passed: the player may have left this screen, which
            // destroys us. Unity's == overload reports a destroyed object as null.
            if (this == null || art == null)
            {
                return;
            }

            // Read straight from the response - never a constant.
            ctaAction = node.cta?.action;

            Build(art);
        }

        private void Build(Texture2D art)
        {
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("PlaySuperTouchpointCanvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvasObject.AddComponent<GraphicRaycaster>();

            // The placement box itself. Anchors are already fractions of the screen, so
            // the widget tracks the same relative spot at any resolution or aspect - no
            // CanvasScaler reference resolution to keep in sync.
            GameObject slotObject = new GameObject("Slot");
            slotObject.transform.SetParent(canvasObject.transform, false);

            RectTransform slot = slotObject.AddComponent<RectTransform>();
            Rect box = Placement;
            slot.anchorMin = new Vector2(box.x, 1f - box.y - box.height);
            slot.anchorMax = new Vector2(box.x + box.width, 1f - box.y);
            slot.offsetMin = Vector2.zero;
            slot.offsetMax = Vector2.zero;

            // The art lives INSIDE the slot, with its own fitter. AspectRatioFitter's
            // FitInParent measures its PARENT and ignores anchors, so the image needs a
            // dedicated container - that container is the slot above. Putting the fitter
            // on the slot itself would have it measure the whole canvas instead.
            GameObject artObject = new GameObject("Art");
            artObject.transform.SetParent(slotObject.transform, false);

            RectTransform artRect = artObject.AddComponent<RectTransform>();
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;

            RawImage image = artObject.AddComponent<RawImage>();
            image.texture = art;

            // Keeps the pill's proportions if the accepted box is not exactly the art's
            // aspect, rather than stretching pixel art.
            AspectRatioFitter fitter = artObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)art.width / art.height;

            // Pattern rule: the entire widget is the tap target - there is no separate
            // CTA button in this shape.
            Button button = artObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OpenStore);
        }

        /// <summary>
        /// A uGUI canvas needs an EventSystem or taps silently do nothing. The game's
        /// scenes may not have one and we cannot edit them to add it.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("PlaySuperEventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private void OpenStore()
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK not initialised - cannot open the store.");
                return;
            }

            // In-app WebView, and it syncs analytics. NEVER Application.OpenURL.
            // cta.action goes through UNCHANGED: the API already appended utm_content,
            // which is how this touchpoint is attributed.
            if (string.IsNullOrEmpty(ctaAction))
            {
                sdk.OpenStore();
                return;
            }

            sdk.OpenStore(ctaAction);
        }

        private void HandleStoreOpened()
        {
            // Duck the game's audio while the store's own content is in front of the
            // player. This moved here from the retired placeholder store button.
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

        protected static async Task<Texture2D> Download(string url)
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
                    Debug.LogWarning("[PlaySuper] Touchpoint art failed to download: " + request.error);
                    return null;
                }

                return DownloadHandlerTexture.GetContent(request);
            }
        }
    }
}
