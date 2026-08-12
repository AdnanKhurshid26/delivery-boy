using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Boot-time PlaySuper wiring for Delivery Boy.
///
/// This is the ONE place the API key and coin id live -- every other script goes
/// through AwardCoins() so a second copy of those values never exists.
///
/// Runs via [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)], which fires at app
/// launch before the first scene loads. That beats every Awake(), survives scene
/// reordering, and needs no GameObject -- so no scene or prefab file is touched.
/// </summary>
public static class PlaySuperBootstrap
{
    // From the PlaySuper console for this game. If the key is ever regenerated,
    // update it here -- it must match the console.
    private const string ApiKey = "56a30f7eaad224eb3b2486c26219f0c2065ee4665c71d28373669f47af101d2a";

    // The single coin linked to this game in PlaySuper.
    private const string RewardCoinId = "75bb3daf-5454-4395-b352-5d0b663bf100";

    // The game's persistent player id. The SAME uuid must be reused every
    // session, otherwise the player looks like a brand new user to PlaySuper
    // and loses their balance.
    private const string PlayerUuidKey = "PlaySuper.PlayerUuid";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // isDev: false -> production API. Switch to true only for dev testing.
        PlaySuperUnitySDK.Initialize(ApiKey, isDev: false);

        // Fire and forget: identity must never block the game from starting.
        _ = SetUpPlayerAsync();
    }

    private static async Task SetUpPlayerAsync()
    {
        try
        {
            bool freshlyMinted;
            string uuid = GetOrCreatePlayerUuid(out freshlyMinted);

            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                // SDK calls return null on error rather than throwing; fail
                // silent-and-absent so gameplay is never gated on PlaySuper.
                return;
            }

            // Register once, on the launch where we first minted the uuid. The
            // endpoint is idempotent, but calling it every launch would cost a
            // network round-trip on every single app start for no reason.
            if (freshlyMinted)
            {
                await sdk.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so only log in when the
            // stored session is missing or expired.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await sdk.LoginFederatedByStudio(uuid);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Player setup skipped: " + e.Message);
        }
    }

    /// <summary>
    /// Returns the persistent player uuid, minting and saving one on first run.
    /// <paramref name="freshlyMinted"/> is true only on the launch that created it.
    /// </summary>
    private static string GetOrCreatePlayerUuid(out bool freshlyMinted)
    {
        string existing = PlayerPrefs.GetString(PlayerUuidKey, string.Empty);
        if (!string.IsNullOrEmpty(existing))
        {
            freshlyMinted = false;
            return existing;
        }

        string uuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(PlayerUuidKey, uuid);
        PlayerPrefs.Save();
        freshlyMinted = true;
        return uuid;
    }

    /// <summary>
    /// Grants PlaySuper coins for an in-game reward moment.
    ///
    /// Safe to call when offline or logged out -- the SDK queues the transaction
    /// locally and syncs it once connectivity and auth are restored. Deliberately
    /// not awaited and never wrapped in blocking error UI: a failed grant must not
    /// interrupt the delivery the player just made.
    /// </summary>
    public static void AwardCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            return;
        }

        _ = sdk.DistributeCoins(RewardCoinId, amount);
    }
}
