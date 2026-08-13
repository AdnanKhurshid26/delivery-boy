using System;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// Single entry point for PlaySuper. Runs before the first scene loads, so it beats
/// every Awake() and needs no GameObject, prefab or scene edit. The SDK spawns its own
/// DontDestroyOnLoad "PlaySuper" object once Initialize is called.
///
/// This class is also the ONLY place the API key and coin id live — every other call
/// site reads them from here rather than hardcoding a second copy.
/// </summary>
public static class PlaySuperBootstrap
{
    private const string ApiKey = "56a30f7eaad224eb3b2486c26219f0c2065ee4665c71d28373669f47af101d2a";

    /// <summary>
    /// The studio's coin, mapped to a delivered order. This is the only coin the game
    /// distributes; stars/lives/speed are progression and are never converted to coins.
    /// </summary>
    public const string DeliveryCoinId = "75bb3daf-5454-4395-b352-5d0b663bf100";

    private const string UuidPrefsKey = "PlaySuper.PlayerUuid";

    public static bool IsReady { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        try
        {
            PlaySuperUnitySDK.Initialize(ApiKey, false, false);
            IsReady = true;
        }
        catch (Exception e)
        {
            // Rewards must never block the game from starting.
            Debug.LogWarning("[PlaySuper] Initialize failed: " + e.Message);
            return;
        }

        // Fire and forget — gameplay never waits on identity. DistributeCoins queues
        // transactions locally while logged out and syncs them once login lands.
        _ = SetUpIdentity();
    }

    private static async Task SetUpIdentity()
    {
        try
        {
            bool minted;
            string uuid = GetOrCreatePlayerUuid(out minted);

            // Only on the launch that mints the id. The endpoint is idempotent, but
            // calling it every launch would cost a needless round trip each time.
            if (minted)
            {
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so skip the round trip if the
            // session is already good.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Identity setup failed: " + e.Message);
        }
    }

    /// <summary>
    /// The game had no persistent player id of its own — SaveSystem only writes
    /// GameData to data.dvb — so PlaySuper mints one and reuses it forever. The SAME
    /// uuid must come back every session or the player loses their balance.
    /// </summary>
    private static string GetOrCreatePlayerUuid(out bool minted)
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

    /// <summary>
    /// Grants coins for a reward moment. Safe to call when the SDK failed to start —
    /// it simply does nothing rather than throwing into gameplay code.
    /// </summary>
    public static void Grant(string coinId, int amount)
    {
        if (!IsReady || amount <= 0) return;

        try
        {
            // Errors self-store locally and retry, so no game-blocking error UI here.
            _ = PlaySuperUnitySDK.Instance.DistributeCoins(coinId, amount);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
        }
    }
}
