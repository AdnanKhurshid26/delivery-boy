using System;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// Persistent player identity for PlaySuper.
    ///
    /// Delivery Boy has no player id of its own -- SaveSystem writes an anonymous GameData
    /// blob and nothing in it identifies the player -- so one is minted here and kept in
    /// PlayerPrefs. The SAME uuid must be reused on every launch, or the player's coin
    /// balance is orphaned.
    /// </summary>
    public static class PlaySuperIdentity
    {
        private const string UuidKey = "PlaySuper.PlayerUuid";

        /// <summary>
        /// Returns this install's persistent uuid, minting and saving one on first call.
        /// <paramref name="minted"/> is true only on the launch that created it, so the
        /// caller can register the player exactly once instead of on every launch.
        /// </summary>
        public static string GetOrCreateUuid(out bool minted)
        {
            string existing = PlayerPrefs.GetString(UuidKey, string.Empty);
            if (!string.IsNullOrEmpty(existing))
            {
                minted = false;
                return existing;
            }

            string created = Guid.NewGuid().ToString();
            PlayerPrefs.SetString(UuidKey, created);
            PlayerPrefs.Save();
            minted = true;
            return created;
        }

        /// <summary>
        /// Registers the player on first launch and logs them in when the stored token has
        /// gone. Never throws: coins queue locally while logged out and sync afterwards, so
        /// a failure here must not gate gameplay or rewards.
        /// </summary>
        public static async Task EnsurePlayerAsync()
        {
            try
            {
                PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
                if (sdk == null)
                {
                    return;
                }

                bool minted;
                string uuid = GetOrCreateUuid(out minted);

                if (minted)
                {
                    // First launch on this install only -- one network round-trip, once.
                    await sdk.CreatePlayerWithUuid(uuid);
                }

                if (!PlaySuperUnitySDK.IsLoggedIn())
                {
                    await sdk.LoginFederatedByStudio(uuid);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] Player identity setup failed: " + e.Message);
            }
        }
    }
}
