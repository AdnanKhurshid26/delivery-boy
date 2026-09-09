using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mounts the "home" touchpoint renderer on the panel that IS the main menu screen.
///
/// menu.unity has no controller script owning the Main menu panel -- each button carries
/// its own script (StartGameButton, TutorialButton, settingsMenu, ExitButton) -- so the
/// one-line mount happens here instead, from code, with no scene edit.
/// </summary>
public static class PlaySuperMenuMount
{
    const string MenuSceneName = "menu";
    const string CanvasName = "Canvas";
    const string PanelName = "Main menu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // menu is the boot scene, so it is already loaded by the time this runs.
        Mount(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Mount(scene);
    }

    static void Mount(Scene scene)
    {
        if (!scene.IsValid() || scene.name != MenuSceneName)
        {
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            if (root.name != CanvasName)
            {
                continue;
            }

            Transform panel = root.transform.Find(PanelName);

            if (panel == null)
            {
                continue;
            }

            if (panel.GetComponent<PlaySuperTouchpoint_Home>() == null)
            {
                panel.gameObject.AddComponent<PlaySuperTouchpoint_Home>();
            }

            return;
        }

        Debug.LogWarning("[PlaySuper] Could not find '" + CanvasName + "/" + PanelName +
                         "' in scene '" + MenuSceneName + "'; the 'home' touchpoint will " +
                         "not render. Was the menu hierarchy renamed?");
    }
}
