using System;
using System.Security.Cryptography;
using System.Text;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// The single source of truth for this game's PlaySuper credentials, and the one place
/// the SDK is initialized. Runs before the first scene loads, so it beats every Awake
/// and needs no GameObject or scene edit.
///
/// No other script should ever hold an API key or a coin id -- they read them from here.
/// </summary>
public static class PlaySuperBootstrap
{
    // Production API key for "Agentic Game", from the PlaySuper console.
    private const string ApiKey = "ps_live_8912e8ce5b84fc14c13f611b1b030ed3dbd5b1a40f1a075713454983cbdd1495";

    // The only coin linked to this game. Every DistributeCoins call site reads this.
    public const string CoinId = "8f702988-bc8c-4448-b81a-58c327f822b7";

    private const string UuidKey = "PlaySuper.PlayerUuid";
    private const string BoundApiKeyKey = "PlaySuper.BoundApiKeyHash";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        // isDev defaults to false -- this is the production key.
        PlaySuperUnitySDK.Initialize(ApiKey);
        SetUpPlayer();
    }

    private static async void SetUpPlayer()
    {
        try
        {
            string currentKeyHash = HashApiKey(ApiKey);
            string storedKeyHash = PlayerPrefs.GetString(BoundApiKeyKey, string.Empty);

            // The stored player and auth token belong to a specific API key. If the key has
            // changed -- or was never recorded, which means this install predates the check --
            // the device is carrying a token minted against a DIFFERENT key. That token still
            // makes IsLoggedIn() return true, so login gets skipped and the store opens
            // unauthenticated with nothing logged. Reset instead.
            if (storedKeyHash != currentKeyHash)
            {
                PlaySuperUnitySDK.Logout();
                PlayerPrefs.DeleteKey(UuidKey);
                PlayerPrefs.Save();
            }

            bool mintedThisLaunch = false;
            string uuid = PlayerPrefs.GetString(UuidKey, string.Empty);

            if (string.IsNullOrEmpty(uuid))
            {
                // The game has no player id of its own -- SaveSystem stores progress only --
                // so mint one once and reuse it for the life of the install.
                uuid = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(UuidKey, uuid);
                PlayerPrefs.Save();
                mintedThisLaunch = true;
            }

            if (mintedThisLaunch)
            {
                // First launch only. The endpoint is idempotent, but calling it every launch
                // costs a round trip for nothing.
                var created = await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
                if (created == null)
                {
                    Debug.LogWarning("[PlaySuper] Player create returned null -- retrying after login.");
                }
            }

            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }

            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                // The uuid is persisted the moment it is generated, so a create that failed
                // (offline first launch, a 4xx) would never be attempted again -- mintedThisLaunch
                // is false forever after. Both SDK calls swallow their errors and return null,
                // so this retry is the only thing standing between that and a permanently
                // anonymous install.
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }

            if (PlaySuperUnitySDK.IsLoggedIn())
            {
                // Record the binding only after a successful login, so a failed reset is
                // retried next launch rather than being marked as done.
                PlayerPrefs.SetString(BoundApiKeyKey, currentKeyHash);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogError("[PlaySuper] Player is not authenticated. Coin grants will queue " +
                               "locally and sync once login succeeds, but the store will open signed out.");
            }
        }
        catch (Exception e)
        {
            // Never let identity setup take the game down with it.
            Debug.LogError("[PlaySuper] Setup failed: " + e.Message);
        }
    }

    private static string HashApiKey(string key)
    {
        using (var sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
