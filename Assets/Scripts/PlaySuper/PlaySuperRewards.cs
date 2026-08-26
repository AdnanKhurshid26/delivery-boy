using System;
using UnityEngine;
using PlaySuperUnity;

/// <summary>
/// Every PlaySuper coin grant in the game goes through here, so the coin id and the
/// payout amount exist in exactly one place.
///
/// Note on the amount: Delivery Boy has no in-game currency to mirror. Its reward is
/// stars (GamePlayManager.calculateScore), which are progression, not a spendable
/// balance - stars are NOT converted into coins anywhere. The 50 below is a studio
/// decision about what a completed level is worth, not a mirrored value.
/// </summary>
public static class PlaySuperRewards
{
    // Agentic Game's single PlaySuper coin.
    private const string CoinId = "7b7309f4-ee73-42ab-88ad-a5e9982e4b47";

    /// <summary>Coins granted for finishing a level.</summary>
    public const int LevelCompleteReward = 50;

    /// <summary>
    /// Grant the level-completion reward. Callers are responsible for making sure
    /// this runs at most once per level - see the playSuperRewardGranted guard in
    /// GamePlayManager, which exists because gameCompleted() has two entry paths.
    /// </summary>
    public static void GrantLevelComplete()
    {
        Grant(LevelCompleteReward);
    }

    /// <summary>
    /// Fire-and-forget coin grant. Never blocks and never surfaces error UI: the SDK
    /// stores failed transactions locally and retries them, so a dropped network
    /// request is not something the player needs to see or the game needs to handle.
    /// </summary>
    public static void Grant(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
            if (sdk == null)
            {
                // Initialize did not run or failed; skip silently rather than
                // interrupting the end-of-level flow.
                Debug.LogWarning("[PlaySuper] SDK unavailable, skipping grant of " + amount + " coins.");
                return;
            }

            _ = sdk.DistributeCoins(CoinId, amount);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] DistributeCoins failed: " + e.Message);
        }
    }
}
