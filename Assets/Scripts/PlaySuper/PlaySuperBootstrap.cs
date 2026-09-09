using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Single entry point for PlaySuper: initializes the SDK once at app launch and
/// wires player identity. Every other call site reads the credentials from here --
/// never hardcode a second key or coin id anywhere else.
///
/// Lives in a standalone static class on [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]
/// so it runs before the first scene loads: it beats every Awake, is immune to scene
/// reordering, and needs no GameObject or scene edit.
/// </summary>
public static class PlaySuperBootstrap
{
    // PRODUCTION credentials for "Agentic Game" (Agentic Studio).
    // A PRODUCTION key is the live credential and opens the store.
    public const string ApiKey = "ps_live_9463403379b18d1a324c1fe18b0fd00770965ff8946abf88dec8cefbedbca134";
    public const string CoinId = "abfea97d-c65d-4541-862d-e6c0d531c795";

    const string UuidPrefKey = "PlaySuper.PlayerUuid";
    const string ApiKeyHashPrefKey = "PlaySuper.ApiKeyHash";

    static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        // isDev: false -> production API. enableAdvertisingId stays off by default;
        // turn it on only behind a tracking-consent flow.
        PlaySuperUnitySDK.Initialize(ApiKey, false);

        SetUpPlayer();
    }

    /// <summary>
    /// Grants coins for a reward moment. Fire-and-forget: the SDK stores failed
    /// transactions locally and retries, so never gate gameplay on this returning.
    /// </summary>
    public static async void GrantCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (PlaySuperUnitySDK.Instance == null)
        {
            Debug.LogWarning("[PlaySuper] GrantCoins called before Initialize; skipping.");
            return;
        }

        await PlaySuperUnitySDK.Instance.DistributeCoins(CoinId, amount);
    }

    static async void SetUpPlayer()
    {
        if (PlaySuperUnitySDK.Instance == null)
        {
            Debug.LogError("[PlaySuper] SDK instance missing after Initialize; player not set up.");
            return;
        }

        // Bind the stored identity to THIS api key, before anything else.
        //
        // Initialize restores an authToken and a cached profile from PlayerPrefs, so
        // IsLoggedIn() can return true while carrying a token minted against a
        // DIFFERENT key -- the login below would be skipped and OpenStore would attach
        // the old key's token to the new key's store, opening unauthenticated with
        // nothing logged. A MISSING stored hash counts as a mismatch, not a fresh
        // install: an install predating this rule holds a token from an unknown key
        // and is exactly the case this rescues.
        string currentKeyHash = HashApiKey(ApiKey);
        bool keyMatches = PlayerPrefs.GetString(ApiKeyHashPrefKey, string.Empty) == currentKeyHash;

        if (!keyMatches)
        {
            PlaySuperUnitySDK.Logout();
            PlayerPrefs.DeleteKey(UuidPrefKey);
            PlayerPrefs.Save();
        }

        bool minted;
        string uuid = LoadOrCreateUuid(out minted);

        // First launch only: registering is idempotent, but repeating it costs a
        // round-trip on every launch.
        if (minted)
        {
            await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
        }

        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
        }

        // "minted" is NOT proof the player exists. The uuid is persisted the moment it
        // is generated, but CreatePlayerWithUuid can fail (offline first launch, a 4xx,
        // the wrong environment) and both SDK calls swallow their errors and return
        // null. Without this retry a single failed create strands the install forever,
        // because minted is false on every later launch and create is never attempted.
        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
            await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
        }

        if (PlaySuperUnitySDK.IsLoggedIn())
        {
            // Record the key only AFTER a successful login, so a failed reset is
            // retried next launch rather than being recorded as done.
            PlayerPrefs.SetString(ApiKeyHashPrefKey, currentKeyHash);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogError("[PlaySuper] Player login failed after retry. This session is " +
                           "anonymous: coin grants will queue locally and sync once login " +
                           "succeeds, but the store will not open authenticated.");
        }
    }

    static string LoadOrCreateUuid(out bool minted)
    {
        string existing = PlayerPrefs.GetString(UuidPrefKey, string.Empty);

        if (!string.IsNullOrEmpty(existing))
        {
            minted = false;
            return existing;
        }

        // This game has no player id of its own -- GameData/SaveSystem persist gameplay
        // state only -- so mint one and reuse the SAME uuid every session.
        string generated = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(UuidPrefKey, generated);
        PlayerPrefs.Save();
        minted = true;
        return generated;
    }

    static string HashApiKey(string key)
    {
        using (SHA256 sha = SHA256.Create())
        {
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(key)));
        }
    }
}
