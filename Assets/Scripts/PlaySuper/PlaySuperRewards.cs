using System;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Every PlaySuper coin payout in the game goes through here.
///
/// Delivery Boy has no in-game currency to mirror -- GameData persists stars,
/// levelUnlocked, healths and speed, and stars are progression rather than a
/// spendable balance. So this amount is a studio decision (a flat grant on level
/// complete), not a 1:1 mirror of an existing wallet, and calculateScore() is
/// deliberately left untouched.
/// </summary>
public static class PlaySuperRewards
{
    /// <summary>Flat grant for finishing a level successfully.</summary>
    public const int LevelCompleteCoins = 50;

    /// <summary>
    /// Grants the level-complete reward. Fire-and-forget: failures are stored
    /// locally by the SDK and retried, so callers must not gate UI on this.
    ///
    /// Callers are responsible for firing this at most once per level -- see the
    /// rewardGranted guard in GamePlayManager.gameCompleted().
    /// </summary>
    public static void GrantLevelComplete()
    {
        Grant(LevelCompleteCoins);
    }

    private static async void Grant(int amount)
    {
        try
        {
            if (PlaySuperUnitySDK.Instance == null)
            {
                return;
            }

            await PlaySuperUnitySDK.Instance.DistributeCoins(PlaySuperBootstrap.CoinId, amount);
        }
        catch (Exception e)
        {
            // Never surface this to the player: a failed payout is retried later.
            Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
        }
    }
}
