using System;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// Every PlaySuper coin grant in the game routes through here.
///
/// Delivery Boy has no in-game currency to mirror: GameData carries healths,
/// speed, levelUnlocked and stars, none of which is a spendable balance. Stars
/// are progression and are never converted into coins. The amount below is the
/// studio's chosen payout, not a mirror of any existing grant.
/// </summary>
public static class PlaySuperRewards
{
    /// <summary>The only coin this game has in PlaySuper.</summary>
    public const string DeliveryCoinId = "9bc164c2-4e27-4348-8ae5-12c7a8c1b396";

    /// <summary>Studio-specified: 10 coins per successful delivery.</summary>
    public const int CoinsPerDelivery = 10;

    /// <summary>
    /// Called once per completed delivery. Safe to call before login — the SDK
    /// queues the transaction locally and syncs it later — and safe to call on
    /// a failure, since errors self-store and retry rather than throwing.
    /// </summary>
    public static void GrantDeliveryReward()
    {
        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                return;
            }

            _ = sdk.DistributeCoins(DeliveryCoinId, CoinsPerDelivery);
        }
        catch (Exception e)
        {
            // Never let a reward call interrupt a delivery.
            Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
        }
    }
}
