using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Single boot entry point for PlaySuper. Runs before the first scene loads, so it beats
/// every Awake and needs no GameObject or scene edit. The SDK spawns its own
/// DontDestroyOnLoad "PlaySuper" object once Initialize is called.
/// </summary>
public static class PlaySuperBootstrap
{
    // The one place these values live. Never hardcode a second key or coin id anywhere else.
    public const string ApiKey = "ps_live_1a1c8135ac1caad2352aa2bef82ffb6436e794de91eae372f67a8a2602584b98";
    public const string CoinId = "dbba6126-6304-42a9-9823-d36baaa12fa7";

    private const string UuidPrefsKey = "PlaySuper.PlayerUuid";
    private const string ApiKeyHashPrefsKey = "PlaySuper.ApiKeyHash";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // isDev: false -> production API. enableAdvertisingId stays false: privacy-default-off,
        // and this game has no tracking-consent flow.
        PlaySuperUnitySDK.Initialize(ApiKey, false, false);

        // Fire and forget. Rewards queue locally while logged out and sync after login,
        // so nothing in the game may wait on this.
        _ = SetUpPlayerAsync();
    }

    private static async Task SetUpPlayerAsync()
    {
        // Bind the stored identity to the API key BEFORE anything else.
        //
        // Initialize restores an authToken and a cached profile from PlayerPrefs, so a device
        // carrying a token minted against a DIFFERENT key reports IsLoggedIn() == true, the
        // login below is skipped, and OpenStore attaches the old key's token to the new key's
        // store - which opens unauthenticated and logs nothing, because the SDK only warns
        // when IsLoggedIn() is false.
        //
        // A MISSING stored hash counts as a mismatch, not as a fresh install: an install that
        // predates this rule holds a token from some unknown key, and that is exactly the case
        // this exists to rescue.
        string currentKeyHash = HashApiKey(ApiKey);
        string storedKeyHash = PlayerPrefs.GetString(ApiKeyHashPrefsKey, string.Empty);

        if (storedKeyHash != currentKeyHash)
        {
            Debug.Log("[PlaySuper] API key changed (or was never recorded) - resetting the local player identity.");
            PlaySuperUnitySDK.Logout();
            PlayerPrefs.DeleteKey(UuidPrefsKey);
            PlayerPrefs.Save();
        }

        bool minted;
        string uuid = LoadOrCreateUuid(out minted);

        // First launch only. The endpoint is idempotent, but repeating it costs a round trip
        // every single launch, so it is gated on having just minted the id.
        if (minted)
        {
            await SafeAwait(PlaySuperUnitySDK.Instance?.CreatePlayerWithUuid(uuid));
        }

        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            await SafeAwait(PlaySuperUnitySDK.Instance?.LoginFederatedByStudio(uuid));
        }

        // "minted" is not proof the player exists: the uuid is persisted the moment it is
        // generated, but CreatePlayerWithUuid can fail silently (offline first launch, a 4xx,
        // the wrong environment) and both SDK calls swallow their errors. Without this retry a
        // single failed create strands the install forever, because minted is false on every
        // later launch and create is never attempted again.
        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            Debug.LogWarning("[PlaySuper] Login did not take - retrying player creation once.");
            await SafeAwait(PlaySuperUnitySDK.Instance?.CreatePlayerWithUuid(uuid));
            await SafeAwait(PlaySuperUnitySDK.Instance?.LoginFederatedByStudio(uuid));
        }

        if (PlaySuperUnitySDK.IsLoggedIn())
        {
            // Recorded only AFTER a successful login, so a failed reset is retried next launch
            // rather than being written down as done.
            PlayerPrefs.SetString(ApiKeyHashPrefsKey, currentKeyHash);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogError("[PlaySuper] Player is NOT logged in after retry. Rewards will queue locally " +
                           "and the store will open unauthenticated until a later launch succeeds.");
        }
    }

    /// <summary>
    /// The game's own persistent player id. Delivery Boy stores no player identity of its own
    /// (SaveSystem persists GameData only), so one is minted here and reused every session.
    /// </summary>
    private static string LoadOrCreateUuid(out bool minted)
    {
        string uuid = PlayerPrefs.GetString(UuidPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(uuid))
        {
            minted = false;
            return uuid;
        }

        uuid = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(UuidPrefsKey, uuid);
        PlayerPrefs.Save();
        minted = true;
        return uuid;
    }

    private static async Task SafeAwait(Task task)
    {
        if (task == null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] SDK call failed: " + e.Message);
        }
    }

    private static string HashApiKey(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
