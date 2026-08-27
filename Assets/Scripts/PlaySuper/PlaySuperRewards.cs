using System;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// Where this game pays out PlaySuper coins.
///
/// Delivery Boy has no in-game currency: GameData holds healths, speed, levelUnlocked, stars,
/// audios and volume - no wallet - and there is no AddCoins-style grant site anywhere in the
/// project. Stars are progression, not spendable balance, so nothing here converts stars into
/// coins at a ratio. The amount and the moment below are a studio decision, recorded once, in
/// one place: change them here rather than at the call site.
/// </summary>
public static class PlaySuperRewards
{
    /// <summary>Coins granted for clearing a level with at least one star.</summary>
    public const int CoinsPerLevelCleared = 100;

    /// <summary>
    /// Called from GamePlayManager.gameCompleted() when the level is cleared with 1+ stars.
    /// The caller owns the once-per-level guard - see GamePlayManager.playSuperRewardGranted.
    /// </summary>
    public static void GrantLevelClearReward()
    {
        Distribute(PlaySuperBootstrap.RewardCoinId, CoinsPerLevelCleared);
    }

    /// <summary>
    /// Fire-and-forget by design. DistributeCoins stores failed transactions locally and retries
    /// them after login, so this must never be wrapped in blocking or game-facing error UI, and
    /// must never gate the level-complete screen.
    /// </summary>
    private static async void Distribute(string coinId, int amount)
    {
        if (amount <= 0 || string.IsNullOrEmpty(coinId))
        {
            return;
        }

        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                Debug.LogWarning("[PlaySuper] SDK instance is null; skipping a " + amount + " coin grant.");
                return;
            }

            await sdk.DistributeCoins(coinId, amount);
        }
        catch (Exception e)
        {
            // Silent-and-absent: a reward problem must never break the screen it fired from.
            Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
        }
    }
}
