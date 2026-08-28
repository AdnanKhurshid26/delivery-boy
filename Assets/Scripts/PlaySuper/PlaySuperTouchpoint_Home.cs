using UnityEngine;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The "home" touchpoint: the OFFER widget on the TASK COMPLETED results panel,
    /// sitting to the right of the star count.
    ///
    /// That panel is not a scene of its own — it is the gameCompletedSceen GameObject that
    /// GamePlayManager switches on — so this renderer is not scene-gated. It is mounted
    /// from gameCompleted(), which is the exact moment the panel becomes visible. It
    /// therefore never appears on the death screen, which goes through gameOver().
    /// </summary>
    public class PlaySuperTouchpoint_Home : PlaySuperTouchpointWidget
    {
        protected override string TouchpointName
        {
            get { return "home"; }
        }

        // Accepted placement: anchor "right", normalized top-left origin.
        protected override Rect Bbox
        {
            get { return new Rect(0.6508143f, 0.6269504f, 0.1218241f, 0.0609929f); }
        }

        /// <summary>
        /// Attaches the widget to the results screen. Safe to call more than once —
        /// gameCompleted() has two entry paths on timed levels.
        /// </summary>
        public static void MountOn(MonoBehaviour screen)
        {
            if (screen == null)
            {
                return;
            }

            if (screen.GetComponent<PlaySuperTouchpoint_Home>() != null)
            {
                return;
            }

            screen.gameObject.AddComponent<PlaySuperTouchpoint_Home>();
        }
    }
}
