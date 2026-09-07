using UnityEngine;

/// <summary>
/// Renderer for the "home" touchpoint - the offer pill on the TASK COMPLETED panel, beside the
/// star count.
///
/// Despite the name, this touchpoint's accepted placement screenshot is the level-complete
/// panel, not the home screen. That panel is a GameObject inside BOTH gameplay scenes
/// (Scene_Base for levels 1-4, NewScene for 5-8), so this is mounted from
/// GamePlayManager.gameCompleted() rather than gated to a scene name: it then works in both
/// and appears only on completion, never on the death screen.
/// </summary>
public class PlaySuperTouchpoint_Home : PlaySuperTouchpointWidget
{
    private const string HostObjectName = "PlaySuperTouchpointHost_home";

    protected override string TouchpointName
    {
        get { return "home"; }
    }

    // Accepted placement, verbatim from PlaySuper: anchor "right", beside the star count.
    protected override Rect Placement
    {
        get
        {
            return new Rect(
                0.650814332247557f,
                0.6269503546099291f,
                0.1218241042345277f,
                0.06099290780141844f);
        }
    }

    /// <summary>
    /// Attaches the widget to the TASK COMPLETED panel. Safe to call more than once:
    /// gameCompleted() is reachable twice on a timed level, and this must not stack widgets.
    /// The host is a child of the panel, so the widget is enabled and disabled with it.
    /// </summary>
    public static void MountOn(GameObject completedPanel)
    {
        if (completedPanel == null)
        {
            return;
        }

        if (completedPanel.GetComponentInChildren<PlaySuperTouchpoint_Home>(true) != null)
        {
            return;
        }

        GameObject host = new GameObject(HostObjectName);
        host.transform.SetParent(completedPanel.transform, false);
        host.AddComponent<PlaySuperTouchpoint_Home>();
    }
}
