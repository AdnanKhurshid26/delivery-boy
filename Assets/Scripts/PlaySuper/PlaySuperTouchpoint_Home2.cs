using UnityEngine;

// Touchpoint `home-2` - approved on the main menu (the DELIVERY BOY title screen in
// Assets/Scenes/menu.unity), sitting below the Start game / Tutorial / Settings / Exit column.
// Mounted by PlaySuperMenuMount.
public class PlaySuperTouchpoint_Home2 : PlaySuperTouchpointWidget
{
    protected override string TouchpointName { get { return "home-2"; } }

    // `home-2`'s own approved placement (anchor: bottom), normalized 0-1, top-left origin.
    protected override Rect Bbox
    {
        get
        {
            return new Rect(
                0.435625f,
                0.7642276422764228f,
                0.121875f,
                0.06097560975609756f);
        }
    }

    public static void MountOn(GameObject screen)
    {
        PlaySuperWidgetMount.Attach<PlaySuperTouchpoint_Home2>(screen, "PlaySuperTouchpoint_home-2");
    }
}
