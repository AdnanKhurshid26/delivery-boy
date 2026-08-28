using System;
using System.Threading.Tasks;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Every PlaySuper payout in the game goes through here.
///
/// Delivery Boy has no in-game currency to mirror - GameData tracks healths, speed,
/// levelUnlocked and a stars[] array, and stars are progression, not a wallet. The studio
/// chose the reward moment and the amount below; nothing is derived from the star count.
/// </summary>
public static class PlaySuperRewards
{
    /// <summary>Flat grant on a completed level, independent of how many stars were earned.</summary>
    public const int LevelCompleteCoins = 100;

    /// <summary>
    /// Grants the level-completion reward. Safe to call from gameplay code: it never blocks,
    /// never throws, and the SDK stores failed transactions locally and retries them, so this
    /// must not be wrapped in game-blocking error UI.
    /// </summary>
    public static void GrantLevelCompleteReward()
    {
        _ = DistributeAsync(PlaySuperBootstrap.CoinId, LevelCompleteCoins);
    }

    private static async Task DistributeAsync(string coinId, int amount)
    {
        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            Debug.LogWarning("[PlaySuper] SDK instance is null - skipping a grant of " + amount + " coins.");
            return;
        }

        try
        {
            Task task = sdk.DistributeCoins(coinId, amount);
            if (task != null)
            {
                await task;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
        }
    }
}
