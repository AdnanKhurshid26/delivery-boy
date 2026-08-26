using System;
using PlaySuperUnity;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hosts the store session on the main menu.
///
/// This used to also build a plain "REWARDS" button as the always-available way
/// into the store. That button was a stand-in until the positioned, art-directed
/// entry point arrived as a PlaySuper touchpoint -- which it now has, as the
/// OFFER widget in PlaySuperTouchpoint_Reward1. Two store entrances on one screen
/// would only compete, so the placeholder is gone.
///
/// What remains is the part that was never about the button: ducking the game's
/// audio while the store WebView is open and restoring it afterwards.
/// </summary>
public class PlaySuperStore : MonoBehaviour
{
    private const string MenuSceneName = "menu";

    private static PlaySuperStore _instance;

    private float _volumeBeforeStore = 1f;
    private bool _subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // AfterSceneLoad has already missed sceneLoaded for the first scene.
        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            Spawn();
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuSceneName)
        {
            Spawn();
        }
    }

    private static void Spawn()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject host = new GameObject("PlaySuperStore");
        _instance = host.AddComponent<PlaySuperStore>();
    }

    private void Awake()
    {
        // Scene-local: a fresh one spawns each time the menu loads.
        SubscribeStoreEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeStoreEvents();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    // OnStoreOpened/OnStoreClosed are STATIC members of PlaySuperUnitySDK, so they
    // are qualified by type rather than through .Instance -- going through the
    // instance raises CS0176. No instance null-check here either: a static event
    // needs no instance, and checking one on the unsubscribe path (which runs from
    // OnDestroy) could skip cleanup and leave a handler pointing at a dead object.
    // _subscribed alone guards both directions.
    private void SubscribeStoreEvents()
    {
        try
        {
            if (_subscribed)
            {
                return;
            }

            PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;
            _subscribed = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Could not subscribe to store events: " + e.Message);
        }
    }

    private void UnsubscribeStoreEvents()
    {
        try
        {
            if (!_subscribed)
            {
                return;
            }

            PlaySuperUnitySDK.OnStoreOpened -= HandleStoreOpened;
            PlaySuperUnitySDK.OnStoreClosed -= HandleStoreClosed;
            _subscribed = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Could not unsubscribe from store events: " + e.Message);
        }
    }

    private void HandleStoreOpened()
    {
        _volumeBeforeStore = AudioListener.volume;
        AudioListener.volume = 0f;
    }

    private void HandleStoreClosed()
    {
        // Restore game state on close rather than assuming the menu's own volume
        // handling will run -- VolumeManager only applies its value on load.
        AudioListener.volume = _volumeBeforeStore;
    }
}
