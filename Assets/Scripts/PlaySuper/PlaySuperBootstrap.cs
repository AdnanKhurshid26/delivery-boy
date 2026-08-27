using System;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// Single entry point for PlaySuper. Runs before the first scene loads, so it beats every
/// Awake, survives scene reordering, and needs no GameObject or scene edit.
///
/// Every other PlaySuper call site in this project reads its credentials from here - there is
/// deliberately no second copy of the api key or coin id anywhere else.
/// </summary>
public static class PlaySuperBootstrap
{
    // Production key for "Agentic Game" (Agentic Studio), resolved from the PlaySuper console.
    public const string ApiKey = "ps_live_679413bfd3d3e0aea54baf3ff25f0fd001fd5182803fcdb95f7961bafbc31c14";

    // The only coin this game has in PlaySuper. See PlaySuperRewards for what is granted.
    public const string RewardCoinId = "a7a5ff33-2830-4ee2-aece-c7d8050075f2";

    private const string UuidPrefKey = "playsuper.player.uuid";
    private const string BoundApiKeyPrefKey = "playsuper.bound.apikey";

    /// <summary>True once Initialize has run. Identity may still be settling asynchronously.</summary>
    public static bool IsInitialized { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        try
        {
            // isDev: false -> production API. enableAdvertisingId: false -> privacy default off;
            // this game has no tracking-consent flow.
            PlaySuperUnitySDK.Initialize(ApiKey, false, false);
            IsInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError("[PlaySuper] Initialize failed: " + e.Message);
            return;
        }

        // Fire-and-forget: identity must never block the menu from appearing. Coin grants queue
        // locally while logged out and sync once login lands, so gameplay is never gated on this.
        _ = EnsurePlayerAsync();
    }

    private static async Task EnsurePlayerAsync()
    {
        try
        {
            ResetIfApiKeyChanged();

            bool minted;
            string uuid = LoadOrCreateUuid(out minted);

            // The create endpoint is idempotent, but calling it every launch costs a round trip,
            // so it normally runs only on the launch that minted the id.
            if (minted)
            {
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
            }

            // The auth token persists in PlayerPrefs, so re-login is skipped when already valid.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }

            // "minted" is NOT proof the player exists. The uuid is persisted the moment it is
            // generated, but CreatePlayerWithUuid can fail (offline first launch, a 4xx, the wrong
            // environment) and both SDK calls swallow their errors and return null. Without this
            // retry, one failed create strands the install forever: minted is false on every later
            // launch, so create is never attempted again.
            if (!PlaySuperUnitySDK.IsLoggedIn())
            {
                await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
                await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
            }

            if (PlaySuperUnitySDK.IsLoggedIn())
            {
                // Recorded only AFTER a successful login, so a reset that failed midway is retried
                // next launch rather than being remembered as done.
                PlayerPrefs.SetString(BoundApiKeyPrefKey, ApiKey);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.LogError(
                    "[PlaySuper] Player is still not authenticated after create + login retry. " +
                    "Rewards will queue locally and sync when login succeeds, but the store will " +
                    "open unauthenticated until then.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[PlaySuper] Player identity setup failed: " + e.Message);
        }
    }

    /// <summary>
    /// Binds the stored player identity to the api key that created it.
    ///
    /// This guards the nastiest and most invisible bug in a PlaySuper integration. Initialize
    /// restores an auth token and cached profile from PlayerPrefs, so after an api key swap
    /// IsLoggedIn() returns TRUE, the login below is skipped, and OpenStore attaches the OLD
    /// key's token to the NEW key's store - which opens unauthenticated and logs no warning,
    /// because the SDK only warns when IsLoggedIn() is false.
    ///
    /// A MISSING stored key counts as a mismatch, not as a fresh install: an install that
    /// predates this rule is carrying a token from some unknown key and is exactly the case
    /// this exists to rescue, so its first boot after this change resets too.
    /// </summary>
    private static void ResetIfApiKeyChanged()
    {
        string boundKey = PlayerPrefs.GetString(BoundApiKeyPrefKey, "");
        if (boundKey == ApiKey)
        {
            return;
        }

        Debug.Log("[PlaySuper] Stored identity was minted against a different (or unknown) api key. Resetting and starting over as a first launch.");

        try
        {
            PlaySuperUnitySDK.Logout();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Logout during identity reset failed: " + e.Message);
        }

        PlayerPrefs.DeleteKey(UuidPrefKey);
        PlayerPrefs.DeleteKey(BoundApiKeyPrefKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// The persistent player id. This game has no existing player identity to reuse - SaveSystem
    /// stores only GameData (stars, unlocked level, audio settings) and has no id field - so a
    /// GUID is minted once here and reused every session thereafter.
    /// </summary>
    private static string LoadOrCreateUuid(out bool minted)
    {
        string uuid = PlayerPrefs.GetString(UuidPrefKey, "");
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
