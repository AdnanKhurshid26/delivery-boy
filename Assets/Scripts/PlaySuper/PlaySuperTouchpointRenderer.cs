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
    /// Shared compositing for Delivery Boy's PlaySuper touchpoints.
    ///
    /// The SDK renders nothing - it returns a node tree and the game builds the UI. Both
    /// of this game's live touchpoints are the "home-entry-widget" pattern: ONE leaf node
    /// carrying a single pre-baked image plus a CTA action, with no title, subtitle, badge,
    /// background, CTA text or CTA art. So this class composites exactly one image and
    /// draws no text - there is deliberately no handling for fields these touchpoints do
    /// not carry.
    ///
    /// Everything is built at runtime on its own overlay canvas. No .unity or .prefab file
    /// is touched, so a touchpoint can be added or removed without a scene merge.
    ///
    /// The config is fetched LIVE on every screen entry. Nothing about the widget's art,
    /// copy or destination is baked in here, so re-styling it in PlaySuper Studio takes
    /// effect without a client build.
    /// </summary>
    public abstract class PlaySuperTouchpointRenderer : MonoBehaviour
    {
        /// <summary>Fetch key - the touchpoint's lowercase-kebab name, also its utm_content.</summary>
        protected abstract string TouchpointName { get; }

        /// <summary>
        /// The accepted placement, normalised 0-1 with a TOP-LEFT origin, as approved by
        /// the studio in PlaySuper Studio.
        /// </summary>
        protected abstract Rect PlacementBbox { get; }

        /// <summary>Above the game's own UI. Override if a screen needs it higher.</summary>
        protected virtual int SortingOrder => 900;

        private string ctaAction;
        private bool audioPausedByStore;

        private void OnEnable()
        {
            // STATIC events - they hang off the type. Subscribing through .Instance is
            // CS0176 and will not compile.
            PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
        }

        private void OnDisable()
        {
            // Static events outlive this component and would keep a dead object alive.
            PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;

            // Never leave the game muted because this object went away while the store
            // was open (e.g. the player hit "Next level" underneath it).
            if (audioPausedByStore)
            {
                AudioListener.pause = false;
                audioPausedByStore = false;
            }
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
                Debug.LogWarning("[PlaySuper] touchpoint '" + TouchpointName +
                                 "' could not be rendered: " + e.Message);
            }
        }

        private async Task BuildAsync()
        {
            // coinId is required - without it the API returns the raw editing view rather
            // than hydrated data. Reuses the game's one coin id, never a new value.
            TouchpointResponse touchpoint =
                await TouchpointManager.GetTouchpointByName(TouchpointName, PlaySuperRewards.CoinId);

            // Every TouchpointManager call returns null on ANY error - they log and never
            // throw - so each of these is a real branch, not defensive noise.
            if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
            {
                return;
            }

            TouchpointNode node = touchpoint.nodes[0];
            if (node == null || node.images == null || node.images.Length == 0 ||
                string.IsNullOrEmpty(node.images[0]))
            {
                return;
            }

            if (node.cta == null || string.IsNullOrEmpty(node.cta.action))
            {
                // Whole widget is the tap target for this pattern, so no action means
                // there is nothing worth showing.
                return;
            }

            Texture2D art = await Download(node.images[0]);
            if (art == null)
            {
                return;
            }

            // The screen may have been torn down while the image downloaded - Unity's
            // null comparison is true once the object is destroyed.
            if (this == null || gameObject == null)
            {
                return;
            }

            ctaAction = node.cta.action;
            Compose(art);
        }

        private void Compose(Texture2D art)
        {
            EnsureEventSystem();

            GameObject canvasObject = new GameObject("PlaySuperTouchpoint_" + TouchpointName);
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvasObject.AddComponent<GraphicRaycaster>();

            // The approved placement, as anchors. The bbox has a top-left origin and
            // RectTransform anchors have a bottom-left one, hence the y flip. Using
            // fractions rather than pixels keeps the widget where the studio put it on
            // any aspect ratio, so no CanvasScaler is needed.
            GameObject placement = new GameObject("Placement");
            placement.transform.SetParent(canvasObject.transform, false);
            RectTransform placementRect = placement.AddComponent<RectTransform>();
            placementRect.anchorMin =
                new Vector2(PlacementBbox.x, 1f - PlacementBbox.y - PlacementBbox.height);
            placementRect.anchorMax =
                new Vector2(PlacementBbox.x + PlacementBbox.width, 1f - PlacementBbox.y);
            placementRect.offsetMin = Vector2.zero;
            placementRect.offsetMax = Vector2.zero;

            // The art gets its OWN container rect. AspectRatioFitter.FitInParent refits to
            // the PARENT rect and ignores sub-region anchors, so putting the fitter on a
            // rect that is itself anchored to the placement stretches the art across the
            // whole screen. Wrapping it is the fix.
            GameObject artObject = new GameObject("Art");
            artObject.transform.SetParent(placement.transform, false);

            RawImage image = artObject.AddComponent<RawImage>();
            image.texture = art;

            RectTransform artRect = artObject.GetComponent<RectTransform>();
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;

            AspectRatioFitter fitter = artObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)art.width / art.height;

            // Card art is pre-baked - nothing is drawn on top of it. This pattern carries
            // no cta.text, so there is no label to composite.
            Button button = artObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(OpenStore);
        }

        /// <summary>
        /// A uGUI canvas needs an EventSystem in the scene or clicks silently do nothing,
        /// and we cannot edit the scene to add one.
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

            // cta.action passes through UNCHANGED: the API already appended utm_content.
            // In-app WebView, never Application.OpenURL - that would skip the WebView and
            // lose the analytics sync.
            sdk.OpenStore(ctaAction);
        }

        private void HandleStoreOpened()
        {
            // Inherited from the placeholder store button this widget replaced.
            if (!AudioListener.pause)
            {
                AudioListener.pause = true;
                audioPausedByStore = true;
            }
        }

        private void HandleStoreClosed()
        {
            // Only undo what we did - the game may have paused audio itself.
            if (audioPausedByStore)
            {
                AudioListener.pause = false;
                audioPausedByStore = false;
            }
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

                return request.result == UnityWebRequest.Result.Success
                    ? DownloadHandlerTexture.GetContent(request)
                    : null;
            }
        }
    }
}
