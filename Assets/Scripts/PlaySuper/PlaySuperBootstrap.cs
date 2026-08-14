using System;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// The single entry point for PlaySuper in this project.
///
/// Runs before the first scene loads, so it beats every Awake, survives scene reordering,
/// and needs no GameObject or scene edit. The SDK spawns its own DontDestroyOnLoad object.
///
/// The API key and coin id live here and ONLY here - every other PlaySuper call site reads
/// them from this class rather than redeclaring them.
/// </summary>
public static class PlaySuperBootstrap
{
    private const string ApiKey = "9c294f58e761f364af192c82c729024ee8acece54a3aeaa2b4b6af99cf293b7f";

    /// <summary>The one coin linked to this game in PlaySuper.</summary>
    public const string CoinId = "eee8a980-c221-45bc-9a1d-5b1838b6570d";

    private const string PlayerUuidKey = "PlaySuperPlayerUuid";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        PlaySuperUnitySDK.Initialize(ApiKey, isDev: false);
        SignIn();
    }

    private static async void SignIn()
    {
        try
        {
            bool minted;
            string uuid = GetOrCreatePlayerUuid(out minted);

            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null) return;

            // Registration is idempotent, but only the launch that mints the uuid needs it -
            // calling it every launch would cost a network round trip for nothing.
            if (minted)
            {
                await sdk.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so only log in when the session is gone.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await sdk.LoginFederatedByStudio(uuid);
            }
        }
        catch (Exception e)
        {
            // Identity problems must never touch gameplay: coin grants queue locally while
            // logged out and sync once authentication is restored.
            Debug.LogWarning("[PlaySuper] sign-in skipped: " + e.Message);
        }
    }

    /// <summary>
    /// The game's own save file (SaveSystem/GameData) carries no player id, so PlaySuper keeps
    /// its own persistent uuid. The SAME value must be reused on every launch.
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
