using System;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// One-time PlaySuper SDK bootstrap. Runs at app launch (before the first scene
/// loads) so it beats every Awake and needs no GameObject or scene edit.
///
/// This is the single source of the game's PlaySuper credentials — every other
/// call site (coin distribution, store button) reads <see cref="CoinId"/> from
/// here rather than hardcoding a second copy.
/// </summary>
public static class PlaySuperBootstrap
{
    // PlaySuper credentials for "Agentic Game" (from the PlaySuper connector).
    private const string ApiKey = "56a30f7eaad224eb3b2486c26219f0c2065ee4665c71d28373669f47af101d2a";

    /// <summary>The reward coin all gameplay rewards are minted in.</summary>
    public const string CoinId = "75bb3daf-5454-4395-b352-5d0b663bf100";

    private const string PlayerUuidKey = "PlaySuperPlayerUuid";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // isDev=false -> production API. enableAdvertisingId stays false (privacy
        // default-off; the game has no tracking-consent flow).
        PlaySuperUnitySDK.Initialize(ApiKey, false, false);

        // Pause/resume game audio while the in-app store WebView is open.
        PlaySuperUnitySDK.OnStoreOpened += HandleStoreOpened;
        PlaySuperUnitySDK.OnStoreClosed += HandleStoreClosed;

        SetUpPlayerIdentity();
    }

    private static async void SetUpPlayerIdentity()
    {
        string uuid = GetOrCreatePlayerUuid(out bool minted);

        var sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            return;
        }

        try
        {
            // Register the player once, on the launch that minted the uuid.
            if (minted)
            {
                await sdk.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so only log in when needed.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await sdk.LoginFederatedByStudio(uuid);
            }
        }
        catch (Exception e)
        {
            // Anonymous-first: never block gameplay or rewards on login. Queued
            // transactions sync once the session is established.
            Debug.LogWarning($"[PlaySuper] Player identity setup deferred: {e.Message}");
        }
    }

    /// <summary>
    /// Returns the game's persistent PlaySuper player id, generating and saving
    /// one on first launch. The same uuid is reused every session.
    /// </summary>
    private static string GetOrCreatePlayerUuid(out bool minted)
    {
        string uuid = PlayerPrefs.GetString(PlayerUuidKey, string.Empty);
        minted = string.IsNullOrEmpty(uuid);
        if (minted)
        {
            uuid = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(PlayerUuidKey, uuid);
            PlayerPrefs.Save();
        }
        return uuid;
    }

    private static void HandleStoreOpened()
    {
        AudioListener.pause = true;
    }

    private static void HandleStoreClosed()
    {
        AudioListener.pause = false;
    }
}
