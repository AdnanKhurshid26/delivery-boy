using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The "home-2" touchpoint: the offer widget on the main menu.
    ///
    /// Screen identified from the accepted placement's screenshot - the "DELIVERY BOY"
    /// title screen with Start game / Tutorial / Settings / Exit, which is
    /// Assets/Scenes/menu.unity (first entry in EditorBuildSettings). The approved spot
    /// is bottom-centre, just below the button stack.
    ///
    /// This widget REPLACES the plain placeholder store button that core setup built on
    /// this same screen - shipping both would put two store entrances on one menu.
    /// </summary>
    public class PlaySuperTouchpoint_Home2 : PlaySuperTouchpointRenderer
    {
        private const string MenuSceneName = "menu";

        protected override string TouchpointName => "home-2";

        // Accepted placement, normalised 0-1 with a top-left origin.
        protected override Rect PlacementBbox =>
            new Rect(0.435625f, 0.764228f, 0.121875f, 0.060976f);

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

            // Returning to the menu re-runs this; one widget only.
            if (FindObjectOfType<PlaySuperTouchpoint_Home2>() != null)
            {
                return;
            }

            new GameObject("PlaySuperTouchpointHome2")
                .AddComponent<PlaySuperTouchpoint_Home2>();
        }
    }
}
