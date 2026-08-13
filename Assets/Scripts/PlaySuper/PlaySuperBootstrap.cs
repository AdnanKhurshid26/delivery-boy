using System;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Single entry point for PlaySuper. Runs before the first scene loads, so it needs
/// no GameObject and survives any scene reordering. Every other PlaySuper call site
/// in this project reads its credentials from here - never hardcode a second key.
/// </summary>
public static class PlaySuperBootstrap
{
    // Credentials for "Agentic Game" (studio: Agentic Studio), issued by PlaySuper.
    public const string ApiKey = "4648b02c680083bf4094a5b474c06d98fbc495b82e0833694dc385feaf985773";

    // The only coin this game may distribute.
    public const string CoinId = "0a798f05-6c5b-4605-8ac1-d5679521b318";

    private const string UuidPrefsKey = "playsuper_player_uuid";

    private static bool hasBooted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static async void Boot()
    {
        if (hasBooted)
        {
            return;
        }

        hasBooted = true;

        try
        {
            // isDev: false -> production. Flip to true only for dev-environment testing.
            PlaySuperUnitySDK.Initialize(ApiKey, isDev: false);

            string uuid = GetOrCreatePlayerUuid(out bool wasJustMinted);

            if (PlaySuperUnitySDK.Instance == null)
            {
                Debug.LogWarning("[PlaySuper] SDK instance unavailable after Initialize; skipping identity setup.");
                return;
            }

            // Only on the launch that minted the id. The endpoint is idempotent, but
            // calling it every launch costs a needless round trip.
            if (wasJustMinted)
            {
                var created = await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
                if (created == null)
                {
                    Debug.LogWarning("[PlaySuper] CreatePlayerWithUuid returned null; login will still be attempted.");
                }
            }

            // The auth token persists in PlayerPrefs, so only log in when we must.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                var session = await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
                if (session == null)
                {
                    Debug.LogWarning("[PlaySuper] Login failed; coin grants will queue locally and sync later.");
                }
            }
        }
        catch (Exception e)
        {
            // PlaySuper must never be able to stop the game from starting.
            Debug.LogWarning("[PlaySuper] Bootstrap failed: " + e.Message);
        }
    }

    /// <summary>
    /// This game has no account system, so the player id is a GUID minted once and
    /// reused every session. The same uuid must be passed to both create and login.
    /// </summary>
    private static string GetOrCreatePlayerUuid(out bool wasJustMinted)
    {
        string existing = PlayerPrefs.GetString(UuidPrefsKey, string.Empty);

        if (!string.IsNullOrEmpty(existing))
        {
            wasJustMinted = false;
            return existing;
        }

        string minted = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(UuidPrefsKey, minted);
        PlayerPrefs.Save();

        wasJustMinted = true;
        return minted;
    }

    /// <summary>
    /// Awards coins for a rewarded action. Safe to fire and forget: the SDK queues
    /// the transaction locally when offline or logged out and syncs it later, so
    /// callers must never gate gameplay on this.
    /// </summary>
    public static async void GrantCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        try
        {
            if (PlaySuperUnitySDK.Instance == null)
            {
                Debug.LogWarning("[PlaySuper] Cannot grant coins before the SDK is initialized.");
                return;
            }

            await PlaySuperUnitySDK.Instance.DistributeCoins(CoinId, amount);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
        }
    }
}
