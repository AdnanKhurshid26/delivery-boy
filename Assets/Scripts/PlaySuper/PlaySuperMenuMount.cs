using UnityEngine;
using UnityEngine.SceneManagement;

// Mounts the `home-2` widget on the main menu.
//
// The menu's screens are panels PRE-PLACED in Assets/Scenes/menu.unity, not prefabs
// instantiated at runtime, and .unity files are off limits - so there is no screen controller
// to add a line to and no component to attach in the editor. This resolves the parent at
// runtime instead. It is a single deterministic lookup once the scene has finished loading, not
// a poll: the panel already exists at that point, because it was serialized into the scene.
public static class PlaySuperMenuMount
{
    const string MenuSceneName = "menu";
    const string MenuPanelName = "Main menu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        // menu.unity is the boot scene, so its load has already happened by the time this runs
        // and sceneLoaded will not fire for it.
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.name == MenuSceneName) MountInto(active);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName) MountInto(scene);
    }

    static void MountInto(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform panel = FindByName(roots[i].transform, MenuPanelName);
            if (panel == null) continue;

            PlaySuperTouchpoint_Home2.MountOn(panel.gameObject);
            return;
        }

        Debug.LogWarning("[PlaySuper] '" + MenuPanelName + "' not found in scene '" + scene.name
            + "'; the home-2 widget was not mounted.");
    }

    // Walks inactive children too, so a panel hidden by SetActive is still found.
    static Transform FindByName(Transform t, string name)
    {
        if (t.name == name) return t;

        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindByName(t.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }
}
