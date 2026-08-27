using System;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Every PlaySuper coin grant in the game goes through here.
///
/// Delivery Boy has no in-game currency of its own -- GameData tracks stars, levels
/// unlocked, healths and speed, all of which are progression rather than a spendable
/// balance. So there is no grant to mirror 1:1; the studio chose this payout directly.
/// </summary>
public static class PlaySuperRewards
{
    /// <summary>Coins awarded for finishing a level. Set by the studio.</summary>
    public const int CoinsPerLevelCompleted = 100;

    /// <summary>
    /// Awards the level-completion coins. Fire and forget: the SDK queues the transaction
    /// locally when the player is offline or not yet logged in and syncs it later, so this
    /// must never block the completion screen or show error UI.
    ///
    /// Callers are responsible for calling this at most once per level -- see the guard in
    /// GamePlayManager.gameCompleted().
    /// </summary>
    public static async void GrantLevelCompleteReward()
    {
        try
        {
            if (PlaySuperUnitySDK.Instance == null)
            {
                Debug.LogWarning("[PlaySuper] SDK instance missing -- level reward skipped.");
                return;
            }

            await PlaySuperUnitySDK.Instance.DistributeCoins(
                PlaySuperBootstrap.CoinId, CoinsPerLevelCompleted);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] Level reward failed: " + e.Message);
        }
    }
}
