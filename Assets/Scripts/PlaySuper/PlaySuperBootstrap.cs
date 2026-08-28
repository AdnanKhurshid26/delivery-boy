using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// Single boot-time entry point for the PlaySuper SDK.
    ///
    /// Runs BeforeSceneLoad so it beats every Awake in the project and needs no
    /// GameObject and no scene edit. The SDK spawns its own DontDestroyOnLoad
    /// "PlaySuper" object once Initialize is called.
    ///
    /// This is the ONLY place the api key and coin id live — never hardcode a
    /// second pair anywhere else in the project.
    /// </summary>
    public static class PlaySuperBootstrap
    {
        public const string ApiKey =
            "ps_live_40981a6ee5ef0fa8a747dec6d4579a6adf2c0ba5f6a57c4ecfcdf239c6ccc119";

        /// <summary>The only PlaySuper coin linked to this game.</summary>
        public const string CoinId = "ca9e7fc2-a9e3-4d3a-8ece-cde4c1431d5d";

        private const string UuidKey = "playsuper_player_uuid";
        private const string BoundApiKeyHashKey = "playsuper_bound_api_key_hash";

        /// <summary>True once Initialize has been called (not the same as logged in).</summary>
        public static bool IsInitialized { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (IsInitialized)
            {
                return;
            }

            try
            {
                // isDev: false -> production API. enableAdvertisingId: false — the game
                // has no tracking-consent flow, so it stays privacy-default-off.
                PlaySuperUnitySDK.Initialize(ApiKey, false, false);
                IsInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[PlaySuper] Initialize failed: " + e.Message);
                return;
            }

            // Fire and forget: rewards queue locally while logged out and sync after
            // login, so nothing in the game should ever wait on this.
            _ = SetUpIdentityAsync();
        }

        private static async Task SetUpIdentityAsync()
        {
            try
            {
                ResetIfBoundToAnotherKey();

                bool minted;
                string uuid = LoadOrCreateUuid(out minted);

                if (PlaySuperUnitySDK.Instance == null)
                {
                    Debug.LogError("[PlaySuper] SDK instance missing; skipping identity setup.");
                    return;
                }

                if (minted)
                {
                    // Idempotent server-side, but skipping it on later launches saves a
                    // round trip every boot.
                    await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
                }

                if (!PlaySuperUnitySDK.IsLoggedIn())
                {
                    await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
                }

                // Both SDK calls swallow their errors and return null, so "minted" is not
                // proof the player exists. If a first launch created the uuid but the
                // create call failed (offline, 4xx), minted is false forever afterwards
                // and the install would be stranded — so retry create once, then login.
                if (!PlaySuperUnitySDK.IsLoggedIn())
                {
                    await PlaySuperUnitySDK.Instance.CreatePlayerWithUuid(uuid);
                    await PlaySuperUnitySDK.Instance.LoginFederatedByStudio(uuid);
                }

                if (PlaySuperUnitySDK.IsLoggedIn())
                {
                    // Written only after a successful login, so a failed reset is retried
                    // next launch instead of being recorded as done.
                    PlayerPrefs.SetString(BoundApiKeyHashKey, HashOf(ApiKey));
                    PlayerPrefs.Save();
                }
                else
                {
                    Debug.LogError(
                        "[PlaySuper] Player is NOT logged in after create + login retry. " +
                        "Coins will queue locally, and the store will open unauthenticated.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[PlaySuper] Identity setup failed: " + e.Message);
            }
        }

        /// <summary>
        /// Binds the stored player to the api key it was minted against.
        ///
        /// Without this, a device carrying a token from a DIFFERENT key reports
        /// IsLoggedIn() == true (Initialize restores the token and a cached profile from
        /// PlayerPrefs), the login below is skipped, and the store opens unauthenticated
        /// with nothing logged — because the SDK only warns when IsLoggedIn() is false.
        ///
        /// A MISSING stored hash counts as a mismatch, not as a fresh install: an install
        /// that predates this code holds a token from some unknown key and is exactly the
        /// case this exists to rescue.
        /// </summary>
        private static void ResetIfBoundToAnotherKey()
        {
            string stored = PlayerPrefs.GetString(BoundApiKeyHashKey, string.Empty);
            if (stored == HashOf(ApiKey))
            {
                return;
            }

            Debug.Log("[PlaySuper] Stored session belongs to a different api key; resetting.");
            try
            {
                PlaySuperUnitySDK.Logout();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] Logout during reset failed: " + e.Message);
            }

            PlayerPrefs.DeleteKey(UuidKey);
            PlayerPrefs.DeleteKey(BoundApiKeyHashKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// The game's own save file (GameData) has no player id, so we keep a persistent
        /// uuid in PlayerPrefs. The SAME uuid must be reused every session.
        /// </summary>
        private static string LoadOrCreateUuid(out bool minted)
        {
            string uuid = PlayerPrefs.GetString(UuidKey, string.Empty);
            if (!string.IsNullOrEmpty(uuid))
            {
                minted = false;
                return uuid;
            }

            uuid = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(UuidKey, uuid);
            PlayerPrefs.Save();
            minted = true;
            return uuid;
        }

        private static string HashOf(string value)
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
}
