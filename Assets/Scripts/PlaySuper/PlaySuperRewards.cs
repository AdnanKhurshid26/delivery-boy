using System;
using System.Threading.Tasks;
using UnityEngine;
using PlaySuperUnity;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The game's PlaySuper coin payouts.
    ///
    /// Delivery Boy has no in-game currency to mirror -- GameData carries healths, speed,
    /// levelUnlocked and stars, none of which is a spendable balance. Stars are progression
    /// and are deliberately NOT converted into coins: a 3-star finish pays exactly the same
    /// as a 1-star finish. The studio chose a flat grant per completed level instead.
    ///
    /// The caller owns the once-per-level guard (see GamePlayManager.playSuperRewardGranted).
    /// </summary>
    public static class PlaySuperRewards
    {
        /// <summary>Flat grant for finishing a level with at least one star.</summary>
        public const int CoinsPerLevelCompleted = 10;

        /// <summary>
        /// Awards the level-completion coins. Fire and forget by design: the SDK stores
        /// failed distributions locally and retries them, so this must never be wrapped in
        /// blocking error UI or allowed to interrupt the game-completed screen.
        /// </summary>
        public static void GrantLevelCompleteReward()
        {
            _ = DistributeAsync(PlaySuperBootstrap.RewardCoinId, CoinsPerLevelCompleted);
        }

        private static async Task DistributeAsync(string coinId, int amount)
        {
            try
            {
                PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
                if (sdk == null)
                {
                    return;
                }

                await sdk.DistributeCoins(coinId, amount);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
            }
        }
    }
}
