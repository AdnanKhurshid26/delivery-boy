using UnityEngine;

// Touchpoint `home` - approved on the Task Completed screen ("TASK COMPLETED / YOU EARNED n"),
// which in this project is GamePlayManager.gameCompletedSceen. Mounted from that controller,
// so the widget's lifetime is the screen's by construction.
public class PlaySuperTouchpoint_Home : PlaySuperTouchpointWidget
{
    protected override string TouchpointName { get { return "home"; } }

    // `home`'s own approved placement (anchor: right), normalized 0-1, top-left origin.
    protected override Rect Bbox
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

    public static void MountOn(GameObject screen)
    {
        PlaySuperWidgetMount.Attach<PlaySuperTouchpoint_Home>(screen, "PlaySuperTouchpoint_home");
    }
}
