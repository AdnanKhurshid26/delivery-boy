using System;
using PlaySuperUnity;
using UnityEngine;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// The game's reward payouts to PlaySuper.
    ///
    /// Delivery Boy has no spendable in-game currency — GameData tracks healths, speed,
    /// levelUnlocked and a stars[] array, and stars are progression, not a wallet. So
    /// nothing here is a mirror of an existing balance: the studio chose a flat payout on
    /// level completion. Stars are never converted into coins.
    /// </summary>
    public static class PlaySuperRewards
    {
        /// <summary>Flat grant on level completion, chosen by the studio.</summary>
        public const int LevelCompleteCoins = 100;

        /// <summary>
        /// Fire-and-forget. The SDK stores failures locally and retries, so this must
        /// never be wrapped in game-blocking error UI and never gate the results screen.
        /// </summary>
        public static async void GrantLevelComplete()
        {
            try
            {
                if (PlaySuperUnitySDK.Instance == null)
                {
                    Debug.LogWarning("[PlaySuper] SDK not ready; skipping level reward.");
                    return;
                }

                await PlaySuperUnitySDK.Instance.DistributeCoins(
                    PlaySuperBootstrap.CoinId, LevelCompleteCoins);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
            }
        }
    }
}
