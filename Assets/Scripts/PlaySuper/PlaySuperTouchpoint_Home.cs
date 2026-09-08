using UnityEngine;
using UnityEngine.SceneManagement;

// The "home" touchpoint: the main menu (Assets/Scenes/menu.unity).
// Mapped from the accepted placement's screenshot, which is the Delivery Boy title screen
// with Start game / Tutorial / Settings / Exit - menu.unity is the only scene that is.
// The approved spot is the pill directly under the Exit button.
public class PlaySuperTouchpoint_Home : PlaySuperTouchpointWidget
{
    const string SceneName = "menu";

    protected override string TouchpointName
    {
        get { return "home"; }
    }

    // Accepted placement bbox, normalized 0-1, top-left origin.
    protected override Rect Bbox
    {
        get { return new Rect(0.435625f, 0.7642276f, 0.121875f, 0.0609756f); }
    }

    // Auto-spawned, so pressing Play is the only manual step and no scene or prefab was
    // edited to place it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSpawn()
    {
        // Covers every later return to the menu (the in-game Exit button loads it again).
        SceneManager.sceneLoaded += OnSceneLoaded;

        // AfterSceneLoad runs once the boot scene is already loaded, so sceneLoaded will
        // not fire for it - handle the current scene directly.
        if (SceneManager.GetActiveScene().name == SceneName)
        {
            Spawn();
        }
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneName)
        {
            Spawn();
        }
    }

    static void Spawn()
    {
        if (FindObjectOfType<PlaySuperTouchpoint_Home>() != null)
        {
            return;
        }

        new GameObject("PlaySuperTouchpoint_home").AddComponent<PlaySuperTouchpoint_Home>();
    }
}
