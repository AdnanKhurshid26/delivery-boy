using System;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// Boots the PlaySuper SDK and establishes this device's player identity.
    ///
    /// This is the ONE place the API key lives - every other call site reads the
    /// SDK singleton, never its own copy of the key.
    ///
    /// It runs BeforeSceneLoad so it beats every Awake() in the project and needs no
    /// GameObject and no scene edit. The SDK spawns its own DontDestroyOnLoad
    /// "PlaySuper" object during Initialize.
    /// </summary>
    public static class PlaySuperBootstrap
    {
        /// <summary>Production API key for "Agentic Game" (Agentic Studio).</summary>
        public const string ApiKey =
            "ps_live_ab98221608f8ed5333ef5f654d4ff5a8e16cb3a1eafa857cc65380fdfd5da847";

        // Delivery Boy has no player id of its own - SaveSystem serialises GameData to
        // data.dvb and GameData has no id field - so we mint one here and keep it for
        // the lifetime of the install.
        private const string UuidPrefsKey = "PlaySuper.PlayerUuid";

        // The API key the stored uuid was minted and logged in against. See ResetIfKeyChanged.
        private const string BoundApiKeyPrefsKey = "PlaySuper.BoundApiKey";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            // isDev: false -> production API. enableAdvertisingId: false -> the game has no
            // tracking-consent flow, so this stays privacy-default-off.
            PlaySuperUnitySDK.Initialize(ApiKey, false, false);

            // Deliberately not awaited: identity work must never delay the menu scene.
            // Rewards earned before login completes are queued locally by the SDK.
            _ = EstablishIdentityAsync();
        }

        private static async Task EstablishIdentityAsync()
        {
            try
            {
                ResetIfKeyChanged();

                bool minted;
                string uuid = LoadOrCreateUuid(out minted);

                PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
                if (sdk == null)
                {
                    Debug.LogError("[PlaySuper] Instance is null after Initialize - " +
                                   "player identity was not established.");
                    return;
                }

                // First launch on this install: register the player once. The endpoint is
                // idempotent, but repeating it costs a round-trip every launch, so it is
                // gated on having just minted the uuid.
                if (minted)
                {
                    await sdk.CreatePlayerWithUuid(uuid);
                }

                // The auth token persists in PlayerPrefs, so skip the login when it is
                // already valid.
                if (!PlaySuperUnitySDK.IsLoggedIn())
                {
                    await sdk.LoginFederatedByStudio(uuid);
                }

                // "Minted" is not proof the player exists: the uuid is persisted the moment
                // it is generated, but CreatePlayerWithUuid can fail silently (offline first
                // launch, a 4xx) and both SDK calls swallow their errors and return null.
                // Without this retry a single failed create would strand the install forever,
                // because minted is false on every later launch.
                if (!PlaySuperUnitySDK.IsLoggedIn())
                {
                    await sdk.CreatePlayerWithUuid(uuid);
                    await sdk.LoginFederatedByStudio(uuid);
                }

                if (PlaySuperUnitySDK.IsLoggedIn())
                {
                    // Recorded only AFTER a successful login, so a failed reset is retried
                    // next launch rather than being written down as done.
                    PlayerPrefs.SetString(BoundApiKeyPrefsKey, ApiKey);
                    PlayerPrefs.Save();
                }
                else
                {
                    Debug.LogError("[PlaySuper] Could not log the player in after a retry. " +
                                   "The store will open unauthenticated and coin grants will " +
                                   "queue locally until a later launch succeeds.");
                }
            }
            catch (Exception e)
            {
                // Identity is best-effort: never let it take the game down.
                Debug.LogError("[PlaySuper] Identity setup threw: " + e);
            }
        }

        /// <summary>
        /// Binds the stored identity to the API key it belongs to.
        ///
        /// Initialize restores an auth token and a cached profile from PlayerPrefs, so a
        /// device still holding a token minted against a DIFFERENT key reports
        /// IsLoggedIn() == true, our login is skipped, and OpenStore attaches the old
        /// key's token to the new key's store - which opens unauthenticated and logs
        /// nothing, because the SDK only warns when IsLoggedIn() is false.
        ///
        /// A MISSING stored key counts as a mismatch, not as a fresh install: an install
        /// that predates this rule is carrying a token from an unknown key and is exactly
        /// the case this exists to rescue.
        /// </summary>
        private static void ResetIfKeyChanged()
        {
            string boundKey = PlayerPrefs.GetString(BoundApiKeyPrefsKey, string.Empty);
            if (boundKey == ApiKey)
            {
                return;
            }

            PlaySuperUnitySDK.Logout();
            PlayerPrefs.DeleteKey(UuidPrefsKey);
            PlayerPrefs.DeleteKey(BoundApiKeyPrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// The install's persistent player id. <paramref name="minted"/> reports whether
        /// this call created it, which is what gates the one-time registration above.
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
    }
}
