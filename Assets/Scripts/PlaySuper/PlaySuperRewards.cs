using UnityEngine;
using PlaySuperUnity;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// Where Delivery Boy pays out PlaySuper coins.
    ///
    /// The game has no in-game currency to mirror: GameData carries healths, speed,
    /// levelUnlocked, stars[] and audio settings, and completing a level awards STARS
    /// via GamePlayManager.calculateScore(). Stars are progression, not a spendable
    /// balance, so they are never converted into coins and are left untouched.
    ///
    /// The studio therefore chose the payout directly: a flat 100 coins for each level
    /// completed. Both values below are the single source of truth - change the payout
    /// here, not at the call site.
    /// </summary>
    public static class PlaySuperRewards
    {
        /// <summary>The only coin "Agentic Game" has in PlaySuper.</summary>
        public const string CoinId = "b678c324-995b-4cdc-b90d-70469105d506";

        /// <summary>Flat grant per completed level, as chosen by the studio.</summary>
        public const int CoinsPerLevelCompleted = 100;

        /// <summary>
        /// Grants the level-completion payout. Safe to call from gameplay code: the SDK
        /// stores failed transactions locally and retries them after login, so this needs
        /// no error UI and must never gate the completion screen.
        ///
        /// The caller is responsible for calling this at most once per level - see the
        /// guard in GamePlayManager.gameCompleted().
        /// </summary>
        public static void GrantLevelCompleteCoins()
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK not initialised - skipping coin grant.");
                return;
            }

            _ = sdk.DistributeCoins(CoinId, CoinsPerLevelCompleted);
        }
    }
}
