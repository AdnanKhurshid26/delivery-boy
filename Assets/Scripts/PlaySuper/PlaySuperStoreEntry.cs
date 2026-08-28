using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Store session audio handling.
///
/// This file used to build a plain placeholder "REWARDS" button on the menu screen during core
/// setup, as the temporary way into the store. The "home" touchpoint now occupies that exact
/// screen, so the button is gone - two store entrances on one menu is not something anyone asked
/// for. What the file still owns is the part the touchpoint does not replace: ducking game audio
/// while the store WebView is up.
///
/// Static on purpose. The SDK's store events are static Actions, so a static subscriber keeps no
/// component alive and needs no unsubscribe.
/// </summary>
public static class PlaySuperStoreEntry
{
    private static bool installed;
    private static float volumeBeforeStore = 1f;

    /// <summary>
    /// Subscribes the store open/close handlers once per session. Every touchpoint renderer
    /// calls this on start; repeat calls are no-ops.
    /// </summary>
    public static void InstallStoreAudio()
    {
        if (installed)
        {
            return;
        }

        installed = true;

        // The store events are STATIC members of the type. Subscribing through .Instance is
        // compile error CS0176.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
    }

    private static void HandleStoreOpened()
    {
        // VolumeSpawn drives AudioListener.volume from the saved GameData, so duck through the
        // same channel and restore the exact value on close.
        volumeBeforeStore = AudioListener.volume;
        AudioListener.volume = 0f;
        AudioListener.pause = true;
    }

    private static void HandleStoreClosed()
    {
        AudioListener.pause = false;
        AudioListener.volume = volumeBeforeStore;
    }
}
