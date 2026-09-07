using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Renderer for the "home-2" touchpoint - the offer pill on the MAIN MENU screen, below the
/// Start game / Tutorial / Settings / Exit buttons.
///
/// Despite the name, this is the touchpoint whose accepted placement screenshot is the actual
/// home screen ("DELIVERY BOY"). It replaces the temporary REWARDS button that core setup
/// built on this same screen.
///
/// Self-installing: no scene or prefab edit, and no line needed in any menu script.
/// </summary>
public class PlaySuperTouchpoint_Home2 : PlaySuperTouchpointWidget
{
    // Boot scene, first entry in EditorBuildSettings.
    private const string MenuSceneName = "menu";
    private const string HostObjectName = "PlaySuperTouchpointHost_home2";

    protected override string TouchpointName
    {
        get { return "home-2"; }
    }

    // Accepted placement, verbatim from PlaySuper: anchor "bottom", below the menu buttons.
    protected override Rect Placement
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // Re-mount whenever the player comes back to the menu from gameplay, not just on the
        // first load.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryMount(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryMount(scene);
    }

    private static void TryMount(Scene scene)
    {
        if (scene.name != MenuSceneName)
        {
            return;
        }

        if (GameObject.Find(HostObjectName) != null)
        {
            return;
        }

        new GameObject(HostObjectName).AddComponent<PlaySuperTouchpoint_Home2>();
    }
}
