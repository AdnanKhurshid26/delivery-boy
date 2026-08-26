using System;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// Single entry point that boots the PlaySuper SDK and establishes player identity.
///
/// Runs at BeforeSceneLoad so it happens before every Awake in the first scene: no
/// GameObject, no scene edit, and immune to anyone reordering the build settings.
/// The SDK spawns its own DontDestroyOnLoad "PlaySuper" object once Initialize runs.
/// </summary>
public static class PlaySuperBootstrap
{
    // Agentic Game's active PlaySuper API key. This is the ONLY place it lives -
    // every other call site goes through PlaySuperUnitySDK.Instance.
    private const string ApiKey =
        "ps_test_11a1c19b6c39117688f3d9483481132887ca9cccccc3c7cd7c363cb9f2682e2d";

    // Delivery Boy had no persistent player id of its own (GameData covers progress
    // only, and SaveSystem writes it to a binary blob), so PlaySuper keeps its own
    // uuid here. It must be the SAME value every session for the player's balance to
    // follow them, hence PlayerPrefs rather than a per-run GUID.
    private const string PlayerUuidKey = "PlaySuper.PlayerUuid";

    private static bool booted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (booted)
        {
            return;
        }
        booted = true;

        try
        {
            // isDev: false -> production API. enableAdvertisingId: false -> privacy
            // default off; flip only alongside a real tracking-consent flow.
            PlaySuperUnitySDK.Initialize(ApiKey, false, false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Initialize failed, rewards disabled this session: " + e.Message);
            return;
        }

        EstablishIdentity();
    }

    /// <summary>
    /// Registers the player once, then logs in when there is no persisted session.
    /// Deliberately fire-and-forget: rewards are anonymous-first, transactions queue
    /// locally while logged out and sync afterwards, so nothing in the game should
    /// ever wait on this or fail because of it.
    /// </summary>
    private static async void EstablishIdentity()
    {
        try
        {
            bool minted;
            string uuid = GetOrCreatePlayerUuid(out minted);

            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                return;
            }

            // Only on the launch that actually minted the uuid. The endpoint is
            // idempotent, but calling it every launch would burn a network round
            // trip on app start for no reason.
            if (minted)
            {
                await sdk.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so re-login only when absent.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await sdk.LoginFederatedByStudio(uuid);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Player identity setup failed: " + e.Message);
        }
    }

    /// <summary>
    /// The player's stable PlaySuper uuid. <paramref name="minted"/> is true only on
    /// the very first call for this install.
    /// </summary>
    public static string GetOrCreatePlayerUuid(out bool minted)
    {
        string existing = PlayerPrefs.GetString(PlayerUuidKey, string.Empty);
        if (!string.IsNullOrEmpty(existing))
        {
            minted = false;
            return existing;
        }

        string uuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(PlayerUuidKey, uuid);
        PlayerPrefs.Save();
        minted = true;
        return uuid;
    }
}
