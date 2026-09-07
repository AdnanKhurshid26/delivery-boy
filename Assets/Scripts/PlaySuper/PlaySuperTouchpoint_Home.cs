using UnityEngine;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The "home" touchpoint: the offer widget on the level-complete panel.
    ///
    /// Screen identified from the accepted placement's screenshot - the
    /// "TASK COMPLETED / YOU EARNED n stars" panel with Restart / Next level / Exit,
    /// which is the gameCompletedSceen object owned by GamePlayManager. The approved spot
    /// is to the right of the star count.
    ///
    /// It mounts from GamePlayManager rather than being gated on a scene name because that
    /// panel is not scene-specific: NextLevelButton sends levels 1-4 to Scene_Base and
    /// levels 5-8 to NewScene, so a scene gate would cover only half the game. Mounting
    /// where the panel is actually shown covers both.
    /// </summary>
    public class PlaySuperTouchpoint_Home : PlaySuperTouchpointRenderer
    {
        protected override string TouchpointName => "home";

        // Accepted placement, normalised 0-1 with a top-left origin.
        protected override Rect PlacementBbox =>
            new Rect(0.650814f, 0.626950f, 0.121824f, 0.060993f);

        /// <summary>
        /// Attaches the widget to the screen controller's object, at most once.
        ///
        /// The idempotency matters: GamePlayManager.gameCompleted() has two entry paths on
        /// a timed level - the last delivery landing early, and the countdown reaching zero
        /// afterwards - so a bare AddComponent would stack two widgets on top of each other.
        /// </summary>
        public static void MountOn(GameObject host)
        {
            if (host == null || host.GetComponent<PlaySuperTouchpoint_Home>() != null)
            {
                return;
            }

            host.AddComponent<PlaySuperTouchpoint_Home>();
        }
    }
}
