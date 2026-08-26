using System;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// Single entry point for PlaySuper. Runs before the first scene loads, so it beats every
/// Awake() and is immune to scene reordering. The SDK spawns its own DontDestroyOnLoad
/// GameObject, so nothing needs to live in a scene.
///
/// This is the ONLY place the apiKey and coin id are declared - every other call site
/// reads them from here.
/// </summary>
public static class PlaySuperBootstrap
{
    /// <summary>
    /// SANDBOX key. This is correct for now: PlaySuper issues the production key once the
    /// SDK has initialized in the running game and one touchpoint has rendered. Note that
    /// OpenStore will not open on a sandbox key - that is expected, not a bug.
    /// </summary>
    public const string ApiKey = "ps_test_11a1c19b6c39117688f3d9483481132887ca9cccccc3c7cd7c363cb9f2682e2d";

    /// <summary>The only coin this game is allowed to distribute.</summary>
    public const string DeliveryCoinId = "7b7309f4-ee73-42ab-88ad-a5e9982e4b47";

    private const string PlayerUuidKey = "PlaySuper.PlayerUuid";

    private static bool isBooted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (isBooted)
        {
            return;
        }

        isBooted = true;

        try
        {
            // isDev: false -> production API. enableAdvertisingId: false -> privacy default,
            // flip only if the studio adds a tracking-consent flow.
            PlaySuperUnitySDK.Initialize(ApiKey, false, false);
            Debug.Log("[PlaySuper] SDK initialized.");
        }
        catch (Exception e)
        {
            // Never let a rewards SDK take the game down at launch.
            Debug.LogWarning("[PlaySuper] Initialize failed: " + e.Message);
            return;
        }

        // Fire and forget: identity is best-effort. DistributeCoins queues transactions
        // locally while logged out and syncs after login, so gameplay never waits on this.
        _ = SetUpPlayerAsync();
    }

    private static async Task SetUpPlayerAsync()
    {
        bool wasMinted;
        string uuid = GetOrCreatePlayerUuid(out wasMinted);

        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                return;
            }

            // Register once, on the launch that actually minted the uuid. The endpoint is
            // idempotent, but calling it every launch costs a needless round trip.
            if (wasMinted)
            {
                await sdk.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so skip the login when already valid.
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
    /// Delivery Boy has no account system - GameData is a local binary blob with no player
    /// id in it - so the uuid is minted once and persisted. The SAME uuid must come back
    /// every session or the player's coin balance detaches from them.
    /// </summary>
    private static string GetOrCreatePlayerUuid(out bool wasMinted)
    {
        string existing = PlayerPrefs.GetString(PlayerUuidKey, string.Empty);
        if (!string.IsNullOrEmpty(existing))
        {
            wasMinted = false;
            return existing;
        }

        string uuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(PlayerUuidKey, uuid);
        PlayerPrefs.Save();

        wasMinted = true;
        return uuid;
    }
}
