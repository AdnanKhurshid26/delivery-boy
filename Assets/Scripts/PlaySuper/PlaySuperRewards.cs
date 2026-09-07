using System;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// The one place this game grants PlaySuper coins.
///
/// This game has no in-game currency to mirror: GameData holds healths, speed,
/// levelUnlocked, stars[], audios and volume, and the only thing awarded on success is
/// stars - which are progression, not a spendable balance, and are never converted into
/// coins. With no currency to match, the studio chose the payout directly: a flat 50
/// coins for completing a level.
/// </summary>
public static class PlaySuperRewards
{
    /// <summary>Flat payout per completed level, chosen by the studio.</summary>
    public const int LevelCompleteCoins = 50;

    /// <summary>Called from GamePlayManager.gameCompleted(), behind a one-shot guard.</summary>
    public static void GrantLevelComplete()
    {
        Grant(LevelCompleteCoins);
    }

    /// <summary>
    /// Fire-and-forget by design. DistributeCoins queues transactions locally when the
    /// player is offline or not yet logged in and syncs them later, so failures are never
    /// surfaced to the player and never allowed to block gameplay.
    /// </summary>
    public static async void Grant(int amount)
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
                Debug.LogWarning("[PlaySuper] SDK instance not ready - skipping grant of " + amount + " coins.");
                return;
            }

            await sdk.DistributeCoins(PlaySuperBootstrap.CoinId, amount);
            Debug.Log("[PlaySuper] Granted " + amount + " coins.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlaySuper] DistributeCoins threw: " + e.Message);
        }
    }
}
