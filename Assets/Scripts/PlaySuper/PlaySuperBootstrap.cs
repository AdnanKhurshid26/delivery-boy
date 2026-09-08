using System;
using System.Security.Cryptography;
using System.Text;
using PlaySuperUnity;
using UnityEngine;

// Single home for PlaySuper credentials and session setup.
// Runs before the first scene loads, so nothing in the project needed a scene edit or a
// startup GameObject. Every other call site reads CoinId from here - the key and coin id
// are never repeated anywhere else.
public static class PlaySuperBootstrap
{
    const string ApiKey = "ps_live_9463403379b18d1a324c1fe18b0fd00770965ff8946abf88dec8cefbedbca134";

    public const string CoinId = "abfea97d-c65d-4541-862d-e6c0d531c795";

    // Flat grant per completed level, as specified by the studio. Delivery Boy has no
    // in-game currency to mirror: stars are progression, not a spendable balance, so this
    // is deliberately independent of calculateScore().
    public const int LevelCompleteCoinReward = 100;

    const string UuidPrefsKey = "PlaySuper.PlayerUuid";
    const string ApiKeyPrefsKey = "PlaySuper.ApiKeyFingerprint";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        PlaySuperUnitySDK.Initialize(ApiKey, false, false);
        SignIn();
    }

    static async void SignIn()
    {
        string fingerprint = Fingerprint(ApiKey);

        // The stored player identity is bound to the API key it was minted against.
        // A mismatch - or NO recorded fingerprint at all, which is an install that predates
        // this rule - means the device may be holding an auth token for a different key.
        // Initialize restores that token, IsLoggedIn() would return true, the login below
        // would be skipped, and OpenStore would attach the old key's token to this key's
        // store: it opens unauthenticated and the SDK logs nothing, because it only warns
        // when IsLoggedIn() is false. So reset and start over as a first launch.
        if (PlayerPrefs.GetString(ApiKeyPrefsKey, string.Empty) != fingerprint)
        {
            PlaySuperUnitySDK.Logout();
            PlayerPrefs.DeleteKey(UuidPrefsKey);
            PlayerPrefs.DeleteKey(ApiKeyPrefsKey);
            PlayerPrefs.Save();
        }

        bool minted;
        string uuid = LoadOrMintUuid(out minted);

        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            Debug.LogError("[PlaySuper] SDK instance unavailable - skipping sign-in.");
            return;
        }

        // CreatePlayerWithUuid is idempotent but costs a round-trip, so only on the launch
        // that actually minted the id.
        if (minted)
        {
            await sdk.CreatePlayerWithUuid(uuid);
        }

        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            await sdk.LoginFederatedByStudio(uuid);
        }

        // "minted" is not proof the player exists on the server. The uuid is persisted the
        // moment it is generated, but CreatePlayerWithUuid can fail (offline first launch,
        // a 4xx) and both SDK calls swallow their errors and return null. Without this
        // retry, one failed create would strand the install forever: minted is false on
        // every later launch, so create would never be attempted again.
        if (!PlaySuperUnitySDK.IsLoggedIn())
        {
            await sdk.CreatePlayerWithUuid(uuid);
            await sdk.LoginFederatedByStudio(uuid);
        }

        if (PlaySuperUnitySDK.IsLoggedIn())
        {
            // Written only after a successful login, so a failed reset is retried next
            // launch rather than being recorded as done.
            PlayerPrefs.SetString(ApiKeyPrefsKey, fingerprint);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogError("[PlaySuper] Sign-in failed after retry. Gameplay is unaffected: "
                + "coin transactions queue locally and sync once a login succeeds.");
        }
    }

    // Fire-and-forget by design: DistributeCoins stores failures locally and retries, so it
    // must never be wrapped in game-blocking error UI.
    public static async void GrantLevelCompleteCoins()
    {
        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            return;
        }

        await sdk.DistributeCoins(CoinId, LevelCompleteCoinReward);
    }

    static string LoadOrMintUuid(out bool minted)
    {
        string stored = PlayerPrefs.GetString(UuidPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(stored))
        {
            minted = false;
            return stored;
        }

        // The game has no player id of its own (SaveSystem writes an unidentified
        // GameData blob and PlayerPrefs only holds SelectedLevel), so mint one once and
        // reuse it every session.
        string fresh = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(UuidPrefsKey, fresh);
        PlayerPrefs.Save();
        minted = true;
        return fresh;
    }

    static string Fingerprint(string key)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
            StringBuilder sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
