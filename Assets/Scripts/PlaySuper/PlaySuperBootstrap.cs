using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

// Single entry point for PlaySuper. Runs at BeforeSceneLoad, so it beats every Awake, is
// immune to scene reordering, and needs no GameObject or scene edit. The SDK spawns its own
// DontDestroyOnLoad "PlaySuper" object.
//
// This is the ONLY place the api key and coin id are written down. Every other call site
// (renderers, reward grants) reads them from here.
public static class PlaySuperBootstrap
{
    public const string ApiKey = "ps_live_9463403379b18d1a324c1fe18b0fd00770965ff8946abf88dec8cefbedbca134";
    public const string CoinId = "abfea97d-c65d-4541-862d-e6c0d531c795";

    const string UuidPrefKey = "playsuper.player.uuid";
    const string BoundKeyPrefKey = "playsuper.player.boundApiKey";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        // isDev = false -> production API. enableAdvertisingId is left at its privacy default
        // (off): this game has no tracking-consent flow.
        PlaySuperUnitySDK.Initialize(ApiKey, false);
        _ = SignIn();
    }

    static async Task SignIn()
    {
        // The stored player is bound to the api key it was minted against. A device carrying a
        // token from a DIFFERENT key reports IsLoggedIn() == true, so the login below would be
        // skipped and the store would open unauthenticated with nothing logged. A MISSING
        // binding counts as a mismatch, not a fresh install - an install predating this rule
        // is exactly the case this rescues.
        string boundKey = PlayerPrefs.GetString(BoundKeyPrefKey, string.Empty);
        if (boundKey != ApiKey)
        {
            PlaySuperUnitySDK.Logout();
            PlayerPrefs.DeleteKey(UuidPrefKey);
            PlayerPrefs.DeleteKey(BoundKeyPrefKey);
            PlayerPrefs.Save();
        }

        bool minted;
        string uuid = LoadOrCreateUuid(out minted);

        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            Debug.LogError("[PlaySuper] SDK instance missing after Initialize; player not signed in.");
            return;
        }

        // The create endpoint is idempotent, but calling it every launch costs a round-trip,
        // so it is only called when this launch actually minted the id.
        if (minted)
        {
            await sdk.CreatePlayerWithUuid(uuid);
        }

        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            await sdk.LoginFederatedByStudio(uuid);
        }

        // "minted" is not proof the player exists: the uuid is persisted the moment it is
        // generated, but CreatePlayerWithUuid can fail (offline first launch, a 4xx) and both
        // SDK calls swallow their errors and return null. Without this retry a single failed
        // create would strand the install forever, because minted is false from then on.
        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            await sdk.CreatePlayerWithUuid(uuid);
            await sdk.LoginFederatedByStudio(uuid);
        }

        if (PlaySuperUnitySDK.IsLoggedIn())
        {
            // Written only AFTER a successful login, so a failed reset is retried next launch
            // rather than being recorded as done.
            PlayerPrefs.SetString(BoundKeyPrefKey, ApiKey);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogError("[PlaySuper] Player login failed after retry - this session is anonymous. "
                + "Coin grants still queue locally and sync once a login succeeds.");
        }
    }

    // Delivery Boy has no player id of its own (GameData persists only healths, speed,
    // levelUnlocked, stars, audios and volume), so one is minted once and reused every session.
    static string LoadOrCreateUuid(out bool minted)
    {
        string stored = PlayerPrefs.GetString(UuidPrefKey, string.Empty);
        if (!string.IsNullOrEmpty(stored))
        {
            minted = false;
            return stored;
        }

        string generated = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(UuidPrefKey, generated);
        PlayerPrefs.Save();
        minted = true;
        return generated;
    }
}
