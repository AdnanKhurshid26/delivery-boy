using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// PlaySuper bootstrap - the single source of truth for this game's PlaySuper
/// credentials and for the player session.
///
/// Runs before the first scene loads, so it beats every Awake, is immune to scene
/// reordering, and needs no GameObject and no scene edit. The SDK spawns its own
/// DontDestroyOnLoad "PlaySuper" object.
/// </summary>
public static class PlaySuperBootstrap
{
    /// <summary>Agentic Game's PRODUCTION API key, from the PlaySuper console.</summary>
    public const string ApiKey = "ps_live_cf87ee012c972fa61d84846e955065c31e59c392abe06994a42794e4aebb44cb";

    /// <summary>
    /// The only coin linked to Agentic Game. Every grant site reads it from here -
    /// it is never redeclared anywhere else in the project.
    /// </summary>
    public const string CoinId = "c164c1dc-2924-4097-8931-48c4de05442d";

    private const string UuidPrefKey = "PlaySuper.PlayerUuid";
    private const string BoundKeyPrefKey = "PlaySuper.BoundApiKey";

    /// <summary>True once Initialize has run.</summary>
    public static bool Initialized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // Fire and forget - BootAsync handles its own errors and must never block startup.
        _ = BootAsync();
    }

    private static async Task BootAsync()
    {
        try
        {
            // Production environment (isDev defaults to false).
            PlaySuperUnitySDK.Initialize(ApiKey);
            Initialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError("[PlaySuper] Initialize failed: " + e.Message);
            return;
        }

        await EnsureSessionAsync();
    }

    private static async Task EnsureSessionAsync()
    {
        // (1) Bind the stored identity to the API key BEFORE trusting IsLoggedIn().
        //     Initialize restores an authToken and a cached profile from PlayerPrefs, so a
        //     device carrying a token minted against a DIFFERENT key still reports
        //     IsLoggedIn() == true. The login below would then be skipped and OpenStore
        //     would attach the old key's token to the new key's store - opening
        //     unauthenticated with nothing logged, because the SDK only warns when
        //     IsLoggedIn() is false.
        //     A MISSING stored key counts as a mismatch on purpose: an install that predates
        //     this rule holds a token from an unknown key and is exactly what this rescues.
        if (PlayerPrefs.GetString(BoundKeyPrefKey, string.Empty) != ApiKey)
        {
            ResetStoredIdentity();
        }

        bool minted;
        string uuid = GetOrCreateUuid(out minted);

        // (2) First launch only. The endpoint is idempotent, but repeating it would cost a
        //     round trip on every single launch.
        if (minted)
        {
            try
            {
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] CreatePlayerWithUuid threw: " + e.Message);
            }
        }

        // (3) Every launch, guarded - the auth token persists in PlayerPrefs.
        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            try
            {
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] LoginFederatedByStudio threw: " + e.Message);
            }
        }

        // (4) "minted" is NOT proof the player exists on the server. The uuid is persisted
        //     the moment it is generated, but CreatePlayerWithUuid can fail (offline first
        //     launch, a 4xx, the wrong environment) and both SDK calls swallow their errors
        //     and return null rather than throwing. Without this retry a single failed
        //     create strands the install forever: minted is false on every later launch, so
        //     create is never attempted again.
        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            try
            {
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] Identity retry threw: " + e.Message);
            }
        }

        if (PlaySuperUnitySDK.IsLoggedIn())
        {
            // Recorded only AFTER a successful login, so a failed reset is retried next
            // launch instead of being remembered as done.
            PlayerPrefs.SetString(BoundKeyPrefKey, ApiKey);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogError("[PlaySuper] Player is NOT authenticated after create + login retry. " +
                           "Coin grants will queue locally and sync once login succeeds, but the " +
                           "store would open anonymously - investigate before shipping.");
        }
    }

    /// <summary>
    /// Clears a session that belongs to a different API key, so the next boot behaves like
    /// a genuine first launch.
    /// </summary>
    private static void ResetStoredIdentity()
    {
        Debug.Log("[PlaySuper] Stored session does not belong to the current API key - resetting identity.");

        try
        {
            // NOTE: PlaySuper's connector playbook documents Logout() as STATIC on the type,
            // while docs.playsuper.club/unity-sdk/core-usage shows Instance.Logout(). This
            // branch was not compiled, so if the compiler reports CS0176 or CS0120 on the
            // next line, THIS ONE LINE is the fix:
            //     PlaySuperUnitySDK.Instance.Logout();
            PlaySuperUnitySDK.Logout();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Logout during identity reset threw: " + e.Message);
        }

        PlayerPrefs.DeleteKey(UuidPrefKey);
        PlayerPrefs.DeleteKey(BoundKeyPrefKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// The game's persistent player id. GameData and SaveSystem store progress only, with no
    /// player identifier of any kind, so one is minted here once and reused every session.
    /// </summary>
    /// <param name="minted">True only on the launch that generated the id.</param>
    private static string GetOrCreateUuid(out bool minted)
    {
        string uuid = PlayerPrefs.GetString(UuidPrefKey, string.Empty);
        if (!string.IsNullOrEmpty(uuid))
        {
            minted = false;
            return uuid;
        }

        uuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(UuidPrefKey, uuid);
        PlayerPrefs.Save();
        minted = true;
        return uuid;
    }
}
