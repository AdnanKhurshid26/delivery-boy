using UnityEngine;

/// <summary>
/// Renderer for the "home" touchpoint.
///
/// Screen: the main menu -- the approved screenshot is the DELIVERY BOY title screen
/// with Start game / Tutorial / Settings / Exit, and the placement sits just below the
/// Exit button (anchor "bottom").
///
/// Mounted by PlaySuperMenuMount on the "Main menu" panel inside menu.unity's root
/// Canvas. That panel is a sibling of "Settings panel", "Quit panel", "Level panel" and
/// "Tutorial panel" -- one active at a time -- so binding to the panel rather than the
/// scene is what keeps the widget off those other screens.
/// </summary>
public class PlaySuperTouchpoint_Home : PlaySuperTouchpointWidget
{
    protected override string TouchpointName
    {
        get { return "home"; }
    }

    // "home"'s OWN accepted placement (normalized 0-1, top-left origin).
    protected override Rect Bbox
    {
        get { return new Rect(0.435625f, 0.7642276422764228f, 0.121875f, 0.06097560975609756f); }
    }

    // menu.unity: root "Canvas" and the nested "Button Canvas" are both m_SortingOrder: 0.
    protected override int HostCanvasSortingOrder
    {
        get { return 0; }
    }
}
