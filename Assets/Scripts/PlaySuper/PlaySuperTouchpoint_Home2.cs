using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The "home-2" touchpoint: the OFFER widget on the DELIVERY BOY main menu, sitting
    /// below the Start game / Tutorial / Settings / Exit stack.
    ///
    /// This REPLACES the plain placeholder store button that core setup put on this same
    /// screen — two store entrances on one menu is not something anyone asked for.
    ///
    /// Self-spawning and gated to the menu scene, so no scene or prefab is edited.
    /// </summary>
    public class PlaySuperTouchpoint_Home2 : PlaySuperTouchpointWidget
    {
        private const string MenuSceneName = "menu";

        protected override string TouchpointName
        {
            get { return "home-2"; }
        }

        // Accepted placement: anchor "bottom", normalized top-left origin.
        protected override Rect Bbox
        {
            get { return new Rect(0.435625f, 0.7642276f, 0.121875f, 0.06097561f); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TrySpawn(scene);
        }

        private static void TrySpawn(Scene scene)
        {
            if (!scene.IsValid() || scene.name != MenuSceneName)
            {
                return;
            }

            if (FindObjectOfType<PlaySuperTouchpoint_Home2>() != null)
            {
                return;
            }

            GameObject host = new GameObject("PlaySuperTouchpoint_Home2");
            host.AddComponent<PlaySuperTouchpoint_Home2>();
        }
    }
}
