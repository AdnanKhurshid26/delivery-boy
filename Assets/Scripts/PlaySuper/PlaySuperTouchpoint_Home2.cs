using UnityEngine;

// The "home-2" touchpoint: the TASK COMPLETED screen.
// Mapped from the accepted placement's screenshot - the panel with YOU EARNED n stars and
// Restart / Next level / Exit - which is GamePlayManager's gameCompletedSceen object, not a
// scene of its own. The approved spot is right of the star count, above Next level.
//
// Mounted on that panel by GamePlayManager.gameCompleted() rather than spawned at scene
// load, so the widget appears with the panel and hides with it.
public class PlaySuperTouchpoint_Home2 : PlaySuperTouchpointWidget
{
    protected override string TouchpointName
    {
        get { return "home-2"; }
    }

    // Accepted placement bbox, normalized 0-1, top-left origin.
    protected override Rect Bbox
    {
        get { return new Rect(0.6508143f, 0.6269504f, 0.1218241f, 0.0609929f); }
    }

    public static void MountOn(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        // gameCompleted() can run twice on a timed level, so never mount twice.
        if (panel.GetComponent<PlaySuperTouchpoint_Home2>() != null)
        {
            return;
        }

        panel.AddComponent<PlaySuperTouchpoint_Home2>();
    }
}
