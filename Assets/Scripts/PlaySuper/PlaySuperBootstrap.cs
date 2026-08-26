using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Boots the PlaySuper SDK and establishes player identity.
///
/// This is the ONLY place the game's PlaySuper credentials live. Every other
/// call site reads CoinId from here rather than declaring its own copy.
///
/// Runs at BeforeSceneLoad so it beats every Awake in the project, survives any
/// reordering of the build settings scene list, and needs no GameObject placed
/// in a scene. The SDK spawns its own DontDestroyOnLoad "PlaySuper" object.
/// </summary>
public static class PlaySuperBootstrap
{
    // Production credentials for "Agentic Game" (Agentic Studio).
    private const string ApiKey = "ps_live_1f89083ef76f4ff2a761f37990151a1c7c2e81b6d320d1aa0e0b626c8b93ee2f";

    /// <summary>The game's linked PlaySuper coin. Read this; never redeclare it.</summary>
    public const string CoinId = "92dcc258-0396-4d7e-91b8-93c40a9eea01";

    // Delivery Boy has no player id of its own -- GameData persists progression
    // (stars, levelUnlocked, healths, speed) and nothing identity-shaped -- so we
    // mint one here and reuse it for the lifetime of the install.
    private const string PlayerUuidKey = "PlaySuper.PlayerUuid";

    private static bool _initialized;

    /// <summary>True once Initialize has run. Call sites null-check the SDK anyway.</summary>
    public static bool IsInitialized
    {
        get { return _initialized; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            // isDev: false -> production API. enableAdvertisingId: false -> the game
            // has no tracking-consent flow, and the SDK is privacy-default-off.
            PlaySuperUnitySDK.Initialize(ApiKey, false, false);
            _initialized = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Initialize failed: " + e.Message);
            return;
        }

        // Identity is awaited internally but never blocks the boot sequence.
        // DistributeCoins queues transactions locally while logged out and syncs
        // after login, so gameplay and rewards must not wait on this.
        SignInAsync();
    }

    private static async void SignInAsync()
    {
        try
        {
            bool minted;
            string uuid = GetOrCreatePlayerUuid(out minted);

            if (PlaySuperUnitySDK.Instance == null)
            {
                return;
            }

            // Registration is idempotent server-side, but calling it every launch
            // would cost a network round-trip each time -- so only on the launch
            // that actually minted the id.
            if (minted)
            {
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs; skip the re-login when it's live.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Sign-in failed, continuing anonymously: " + e.Message);
        }
    }

    /// <summary>
    /// Returns the persistent player uuid, minting one on first ever launch.
    /// <paramref name="minted"/> reports whether this call created it.
    /// </summary>
    private static string GetOrCreatePlayerUuid(out bool minted)
    {
        string uuid = PlayerPrefs.GetString(PlayerUuidKey, string.Empty);

        if (!string.IsNullOrEmpty(uuid))
        {
            minted = false;
            return uuid;
        }

        uuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(PlayerUuidKey, uuid);
        PlayerPrefs.Save();
        minted = true;
        return uuid;
    }
}
