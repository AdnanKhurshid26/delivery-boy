using System;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// The game's reward moments, mirrored into PlaySuper coins.
///
/// Delivery Boy has no in-game currency - stars are progression, not a spendable balance,
/// and are never converted into coins. The studio specified this payout directly:
/// 50 coins on level complete, only when the run earns at least one star.
/// </summary>
public static class PlaySuperRewards
{
    /// <summary>Studio-specified payout for completing a level with 1+ stars.</summary>
    public const int CoinsPerLevelComplete = 50;

    /// <summary>
    /// Grants the level-complete reward. Safe to call from gameplay code: it never throws
    /// and never blocks. The SDK stores failed transactions locally and retries them, so
    /// there is deliberately no error UI here.
    ///
    /// CALLERS MUST GUARD AGAINST REPEAT CALLS - see GamePlayManager.rewardGranted.
    /// </summary>
    public static async void GrantLevelCompleteReward()
    {
        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK not ready; skipping level-complete reward.");
                return;
            }

            await sdk.DistributeCoins(PlaySuperBootstrap.DeliveryCoinId, CoinsPerLevelComplete);
            Debug.Log("[PlaySuper] Granted " + CoinsPerLevelComplete + " coins for level complete.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
        }
    }
}
