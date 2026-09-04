using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The "home-2" touchpoint: the offer pill on the main menu, below the
    /// Start game / Tutorial / Settings / Exit column.
    ///
    /// Screen mapping: the accepted placement's screenshot IS the DELIVERY BOY title
    /// screen with that button column, which is menu.unity - the first scene in
    /// EditorBuildSettings and the same screen the retired placeholder store button
    /// targeted. This widget replaces that placeholder.
    ///
    /// Persistent by design (the pattern has no close control), and self-mounting, so
    /// menu.unity is never edited: pressing Play is the only step.
    /// </summary>
    public class PlaySuperTouchpointMenu : PlaySuperTouchpointWidget
    {
        private const string MenuSceneName = "menu";

        protected override string TouchpointName => "home-2";

        // Accepted placement, normalized top-left origin: anchor "bottom", centred
        // under the menu buttons.
        protected override Rect Placement =>
            new Rect(0.435625f, 0.7642276f, 0.121875f, 0.0609756f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TrySpawn(SceneManager.GetActiveScene().name);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TrySpawn(scene.name);
        }

        private static void TrySpawn(string sceneName)
        {
            if (sceneName != MenuSceneName)
            {
                return;
            }

            // Returning to the menu from a level re-enters this path; one is enough.
            if (FindObjectOfType<PlaySuperTouchpointMenu>() != null)
            {
                return;
            }

            new GameObject("PlaySuperTouchpointMenu").AddComponent<PlaySuperTouchpointMenu>();
        }
    }
}
