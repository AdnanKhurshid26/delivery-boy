using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Renders the "reward-1" touchpoint -- the OFFER button that sits in the main menu's
/// button stack, between Start game and Settings.
///
/// Pattern: home-entry-widget (one leaf node, art plus a CTA, no children). The live
/// response carries images[0] and cta.action only -- no cta.text and no cta.backgroundImage,
/// so the label is baked into the art and there is nothing to composite on top of it. The
/// whole widget is the tap target.
///
/// The config is fetched live every time. Nothing about the node is hardcoded here beyond
/// the touchpoint's name and its accepted placement.
/// </summary>
public class PlaySuperTouchpointReward1 : MonoBehaviour
{
    // Doubles as the SDK fetch key and the utm_content value on every click.
    private const string TouchpointName = "reward-1";

    // The accepted placement, normalized 0-1 with a TOP-LEFT origin.
    private static readonly Rect Placement =
        new Rect(0.431875f, 0.5176151761517616f, 0.131875f, 0.04471544715447155f);

    /// <summary>
    /// Raised when the touchpoint cannot be shown -- fetch failed, came back empty, or the
    /// art would not download. The menu uses this to fall back to a plain store button so
    /// the player is never left with no way into the store.
    /// </summary>
    public Action OnUnavailable;

    private GameObject canvasObject;
    private bool resolved;

    private async void Start()
    {
        TouchpointResponse touchpoint = null;

        try
        {
            touchpoint = await TouchpointManager.GetTouchpointByName(
                TouchpointName, PlaySuperBootstrap.CoinId);
        }
        catch (Exception e)
        {
            // The SDK logs and returns null rather than throwing, but a touchpoint must
            // never be able to take the menu down with it.
            Debug.LogWarning("[PlaySuper] Touchpoint fetch failed: " + e.Message);
        }

        // The player may have left the menu while this was in flight.
        if (this == null || gameObject == null)
        {
            return;
        }

        if (touchpoint == null || touchpoint.nodes == null || touchpoint.nodes.Length == 0)
        {
            ReportUnavailable();
            return;
        }

        var node = touchpoint.nodes[0];

        string artUrl = node.images != null && node.images.Length > 0 ? node.images[0] : null;
        string action = node.cta != null ? node.cta.action : null;

        if (string.IsNullOrEmpty(artUrl) || string.IsNullOrEmpty(action))
        {
            ReportUnavailable();
            return;
        }

        Texture2D art = await Download(artUrl);

        if (this == null || gameObject == null)
        {
            return;
        }

        if (art == null)
        {
            ReportUnavailable();
            return;
        }

        Build(art, action);
    }

    private void ReportUnavailable()
    {
        if (resolved)
        {
            return;
        }

        resolved = true;

        var callback = OnUnavailable;
        if (callback != null)
        {
            callback();
        }
    }

    private void Build(Texture2D art, string action)
    {
        resolved = true;

        canvasObject = new GameObject("PlaySuperTouchpointCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        canvasObject.AddComponent<GraphicRaycaster>();

        // Container anchored to the accepted placement box. The bbox has a top-left origin
        // and uGUI anchors run from the bottom left, so y is flipped.
        var container = new GameObject("Widget", typeof(RectTransform));
        container.transform.SetParent(canvasObject.transform, false);

        var containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(
            Placement.x, 1f - Placement.y - Placement.height);
        containerRect.anchorMax = new Vector2(
            Placement.x + Placement.width, 1f - Placement.y);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // The art goes in a CHILD of that container, not on it. AspectRatioFitter's
        // FitInParent refits to its PARENT and ignores sub-region anchors -- putting the
        // fitter on the anchored rect itself would blow the widget up to the whole screen.
        var artObject = new GameObject("Art", typeof(RectTransform));
        artObject.transform.SetParent(container.transform, false);

        var artRect = artObject.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero;
        artRect.anchorMax = Vector2.one;
        artRect.offsetMin = Vector2.zero;
        artRect.offsetMax = Vector2.zero;

        var rawImage = artObject.AddComponent<RawImage>();
        rawImage.texture = art;

        var fitter = artObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = art.height == 0 ? 1f : (float)art.width / art.height;

        // home-entry-widget: the whole widget is the tap target, no close control.
        var button = artObject.AddComponent<Button>();
        button.targetGraphic = rawImage;
        button.onClick.AddListener(() => OpenStore(action));
    }

    private static void OpenStore(string action)
    {
        if (PlaySuperUnitySDK.Instance == null)
        {
            Debug.LogWarning("[PlaySuper] SDK instance missing -- store not opened.");
            return;
        }

        // Pass the action through UNCHANGED. The API already appended
        // ?utm_content=reward-1, and rewriting the url breaks click attribution.
        // Always the in-app WebView, never Application.OpenURL.
        PlaySuperUnitySDK.Instance.OpenStore(action);
    }

    private static async Task<Texture2D> Download(string url)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            var operation = request.SendWebRequest();

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
