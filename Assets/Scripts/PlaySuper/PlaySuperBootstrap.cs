using System;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// Single entry point for PlaySuper initialization.
///
/// Lives here rather than in an existing manager's Awake() because
/// BeforeSceneLoad runs at app launch ahead of every scene's Awake, is immune
/// to scene reordering, and needs no GameObject or scene edit. The SDK spawns
/// its own DontDestroyOnLoad "PlaySuper" object once Initialize is called.
///
/// This is the ONLY place the apiKey appears. Every other call site reads its
/// coin ids from PlaySuperRewards.
/// </summary>
public static class PlaySuperBootstrap
{
    private const string ApiKey = "e1a601cba8083ee191deae3fee88049664638bdf2378582474ca67dceb97ba7b";

    // The game had no persistent player id of its own (SaveSystem writes a
    // binary GameData blob with no identity field), so one is minted here and
    // reused for every session thereafter.
    private const string UuidPrefsKey = "PlaySuperPlayerUuid";

    private static bool hasBooted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (hasBooted)
        {
            return;
        }
        hasBooted = true;

        try
        {
            PlaySuperUnitySDK.Initialize(ApiKey, false, false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Initialize failed: " + e.Message);
            return;
        }

        // Fire-and-forget: identity work must never gate startup or gameplay.
        // Coin grants queue locally while logged out and sync after login.
        _ = SetUpPlayerAsync();
    }

    private static async Task SetUpPlayerAsync()
    {
        try
        {
            bool wasMinted;
            string uuid = GetOrCreateUuid(out wasMinted);

            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                return;
            }

            // Registration is idempotent server-side, but calling it on every
            // launch would cost a network round-trip each time, so it runs only
            // on the launch that actually created the uuid.
            if (wasMinted)
            {
                await sdk.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so skip re-login.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await sdk.LoginFederatedByStudio(uuid);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Player setup failed: " + e.Message);
        }
    }

    private static string GetOrCreateUuid(out bool wasMinted)
    {
        string existing = PlayerPrefs.GetString(UuidPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(existing))
        {
            wasMinted = false;
            return existing;
        }

        string uuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(UuidPrefsKey, uuid);
        PlayerPrefs.Save();
        wasMinted = true;
        return uuid;
    }
}
