using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Puts a touchpoint widget into the screen's OWN canvas.
//
// This is what stops a widget appearing on screens it does not belong to, and it is not a
// sortingOrder problem: a widget can only be occluded by things drawn in the same canvas as
// itself. menu.unity layers its screens as sibling panels inside one root Canvas (Main menu,
// Settings panel, Quit panel, Level panel, Tutorial panel), so a canvas of our own would draw
// over all of them or under all of them, whatever order we gave it. It would also cost a draw
// call on a frame budget that does not have spare ones.
public static class PlaySuperWidgetMount
{
    public static GameObject Attach<T>(GameObject screen, string widgetName) where T : PlaySuperTouchpointWidget
    {
        if (screen == null) return null;

        RectTransform screenRect = screen.transform as RectTransform;
        if (screenRect == null)
        {
            Debug.LogWarning("[PlaySuper] " + widgetName + ": '" + screen.name + "' has no RectTransform; not mounted.");
            return null;
        }

        // Mount once per screen. Both call sites can fire more than once - gameCompleted() is
        // reachable twice, and the menu scene can be reloaded.
        PlaySuperScreenVisibilityBinder binder = screen.GetComponent<PlaySuperScreenVisibilityBinder>();
        if (binder != null && binder.Widget != null) return binder.Widget;

        Canvas canvas = screen.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[PlaySuper] " + widgetName + ": no Canvas above '" + screen.name + "'; not mounted.");
            return null;
        }

        Canvas root = canvas.rootCanvas;
        RectTransform rootRect = root != null ? root.transform as RectTransform : null;
        if (rootRect == null) return null;

        GameObject go = new GameObject(widgetName, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();

        // Parent to the CANVAS ROOT, not to the screen's panel: a ScreenSpaceOverlay canvas IS
        // the screen, and the bbox is a fraction of a full-screen screenshot. Panels are
        // routinely small centred rects, so 0-1 anchors inside one land nowhere near the
        // approved spot.
        rt.SetParent(rootRect, false);
        rt.localScale = Vector3.one;

        // Then order it against the screen's TOP-LEVEL node - the last ancestor still directly
        // under that canvas, which is not necessarily the panel we were handed. A sibling index
        // taken from a nested panel orders the widget among its container's children and says
        // nothing about the other screens.
        Transform topLevel = screenRect;
        while (topLevel.parent != null && topLevel.parent != rootRect)
        {
            topLevel = topLevel.parent;
        }

        if (topLevel == rootRect)
        {
            // The screen is the root canvas itself. There is no sibling to order against, and
            // anything covering it lives in another canvas ordered by sortingOrder - which this
            // widget inherits by living inside this one.
            rt.SetAsLastSibling();
        }
        else
        {
            rt.SetSiblingIndex(topLevel.GetSiblingIndex() + 1);
        }

        go.AddComponent<T>();

        // Layering is not the only way games switch screens: menu.unity deactivates its panels
        // (Settings / Quit / Level / Tutorial ship inactive), and a deactivated screen draws
        // nothing over the widget to hide it. This companion covers that style; the sibling
        // index above covers a screen that stays active under a later panel.
        if (binder == null) binder = screen.AddComponent<PlaySuperScreenVisibilityBinder>();
        binder.Bind(go);

        EnsureEventSystem();
        return go;
    }

    // A uGUI Canvas needs an EventSystem in the scene or clicks silently do nothing.
    static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
