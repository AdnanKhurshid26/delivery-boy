using UnityEngine;

/// <summary>
/// Renderer for the "home-2" touchpoint.
///
/// Screen: the TASK COMPLETED results screen -- the approved screenshot shows
/// "YOU EARNED n *" with Restart / Next level / Exit, and the placement sits to the
/// right of the star count (anchor "right").
///
/// Mounted by GamePlayManager on the TaskCompletedScreen prefab instance, which starts
/// inactive and is flipped on by gameCompleted(). Its own SetActive therefore drives
/// OnEnable/OnDisable, so the widget appears exactly when the results screen does.
/// GamePlayManager lives in both gameplay scenes (Scene_Base for levels 1-4, NewScene
/// for 5-8), so this is mounted in both.
/// </summary>
public class PlaySuperTouchpoint_Home2 : PlaySuperTouchpointWidget
{
    protected override string TouchpointName
    {
        get { return "home-2"; }
    }

    // "home-2"'s OWN accepted placement (normalized 0-1, top-left origin).
    protected override Rect Bbox
    {
        get
        {
            return new Rect(0.650814332247557f, 0.6269503546099291f,
                            0.1218241042345277f, 0.06099290780141844f);
        }
    }

    // Scene_Base / NewScene: every canvas is m_SortingOrder: 0 apart from negative
    // background layers, and the TaskCompletedScreen prefab's own canvas is 0 too.
    protected override int HostCanvasSortingOrder
    {
        get { return 0; }
    }
}
