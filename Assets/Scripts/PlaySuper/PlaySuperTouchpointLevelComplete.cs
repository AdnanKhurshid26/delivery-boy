using UnityEngine;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The "home" touchpoint: the offer pill on the TASK COMPLETED panel, to the right
    /// of the star count and above the Restart / Next level / Exit row.
    ///
    /// Screen mapping: the accepted placement's screenshot IS that completion panel -
    /// GamePlayManager.gameCompletedSceen - not a scene of its own. Both gameplay
    /// scenes show it (NextLevelButton loads Scene_Base for levels 1-4 and NewScene
    /// for 5-8, and both run a GamePlayManager), so mounting is triggered from
    /// gameCompleted() rather than gated on a scene name: that one call site covers
    /// both scenes and fires only when the panel is actually on screen.
    ///
    /// Note the name: "home" is the SDK fetch key and the utm_content on every click,
    /// so clicks from this level-completion pill will report as "home" in attribution.
    /// Renaming the touchpoint in PlaySuper is a plain update if the studio wants a
    /// label that reads truer - the constant below would then change with it.
    /// </summary>
    public class PlaySuperTouchpointLevelComplete : PlaySuperTouchpointWidget
    {
        protected override string TouchpointName => "home";

        // Accepted placement, normalized top-left origin: anchor "right", beside the
        // earned-stars line.
        protected override Rect Placement =>
            new Rect(0.6508143f, 0.6269504f, 0.1218241f, 0.0609929f);

        /// <summary>
        /// Called by GamePlayManager when the completion panel is shown. Idempotent:
        /// gameCompleted() can run twice on a timed level finished early, and the widget
        /// dies with the scene, so there is nothing to clean up between levels.
        /// </summary>
        public static void Mount()
        {
            if (FindObjectOfType<PlaySuperTouchpointLevelComplete>() != null)
            {
                return;
            }

            new GameObject("PlaySuperTouchpointLevelComplete")
                .AddComponent<PlaySuperTouchpointLevelComplete>();
        }
    }
}
