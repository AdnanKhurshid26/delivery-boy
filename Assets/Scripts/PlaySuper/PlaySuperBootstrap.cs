using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Initializes the PlaySuper SDK and establishes player identity at app launch.
///
/// Runs BeforeSceneLoad so it beats every Awake in the first scene, is immune to
/// scene reordering, and needs no GameObject or scene edit. The SDK spawns its
/// own DontDestroyOnLoad "PlaySuper" object.
/// </summary>
public static class PlaySuperBootstrap
{
    private const string UuidKey = "PlaySuper.PlayerUuid";
    private const string BoundApiKeyKey = "PlaySuper.BoundApiKey";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // isDev: false  -> production API.
        // enableAdvertisingId: false -> privacy-default-off; the game has no
        // tracking-consent flow.
        PlaySuperUnitySDK.Initialize(PlaySuperConfig.ApiKey, false, false);

        // Fire-and-forget: identity resolves in the background. Rewards queue
        // locally while logged out and sync afterwards, so gameplay never waits
        // on this.
        _ = EnsureIdentityAsync();
    }

    private static async Task EnsureIdentityAsync()
    {
        try
        {
            ResetIfKeyChanged();

            bool minted;
            string uuid = GetOrCreateUuid(out minted);

            // First launch only - the endpoint is idempotent, but repeating it
            // costs a round-trip every single launch.
            if (minted)
            {
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so guard to avoid re-login.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }

            // "Minted" is not proof the player exists on the server. The uuid is
            // persisted the moment it is generated, but CreatePlayerWithUuid can
            // fail (offline first launch, a 4xx, wrong environment) and BOTH SDK
            // calls swallow their errors and return null. Without this retry a
            // single failed create strands the install forever: minted is false
            // on every later launch, so create is never attempted again.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }

            if (PlaySuperUnitySDK.IsLoggedIn())
            {
                // Recorded only AFTER a successful login, so a failed reset is
                // retried next launch instead of being marked done.
                PlayerPrefs.SetString(BoundApiKeyKey, PlaySuperConfig.ApiKey);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogError(
                    "[PlaySuper] Could not establish player identity. This session is " +
                    "anonymous: coin grants will queue locally and sync once login " +
                    "succeeds, and the store will open unauthenticated.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[PlaySuper] Identity setup failed: " + e);
        }
    }

    /// <summary>
    /// Binds the stored player identity to the API key that minted it.
    ///
    /// On a mismatch the device is carrying a player and an auth token created
    /// against a DIFFERENT key, so everything is wiped and the next boot behaves
    /// like a first launch.
    ///
    /// A MISSING stored key counts as a mismatch, not as a fresh install - an
    /// install predating this rule holds a token from an unknown key and is the
    /// exact case this exists to rescue.
    ///
    /// Why this matters: Initialize restores authToken plus a cached profile from
    /// PlayerPrefs, so IsLoggedIn() returns TRUE, the login below is skipped, and
    /// OpenStore attaches the OLD key's token to the NEW key's store. It opens
    /// unauthenticated and logs nothing, because the SDK only warns when
    /// IsLoggedIn() is false.
    /// </summary>
    private static void ResetIfKeyChanged()
    {
        string boundKey = PlayerPrefs.GetString(BoundApiKeyKey, string.Empty);
        if (boundKey == PlaySuperConfig.ApiKey)
        {
            return;
        }

        Debug.Log("[PlaySuper] Stored identity was minted under a different API key. Resetting.");
        PlaySuperUnitySDK.Logout();
        PlayerPrefs.DeleteKey(UuidKey);
        PlayerPrefs.DeleteKey(BoundApiKeyKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// The game's persistent player id. Delivery Boy's SaveSystem stores progress
    /// but no player identity, so one is generated once and reused every session.
    /// </summary>
    private static string GetOrCreateUuid(out bool minted)
    {
        string uuid = PlayerPrefs.GetString(UuidKey, string.Empty);
        if (!string.IsNullOrEmpty(uuid))
        {
            minted = false;
            return uuid;
        }

        uuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(UuidKey, uuid);
        PlayerPrefs.Save();
        minted = true;
        return uuid;
    }
}
