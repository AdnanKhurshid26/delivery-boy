using System;
using PlaySuperUnity;
using UnityEngine;

/// <summary>
/// Grants PlaySuper coins at the game's reward moment.
///
/// This game has no in-game currency to mirror - GameData tracks healths, speed,
/// levelUnlocked and stars, and stars are progression rather than spendable balance,
/// so they are never distributed or converted. The amount and trigger below were
/// specified by the studio: 10 coins when a level is completed.
/// </summary>
public static class PlaySuperRewards
{
    private const int LevelCompleteAmount = 10;

    public static void GrantLevelComplete()
    {
        Distribute(LevelCompleteAmount);
    }

    private static async void Distribute(int amount)
    {
        try
        {
            PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;

            // SDK accessors return null on error rather than throwing - fail silent and absent.
            if (sdk == null) return;

            await sdk.DistributeCoins(PlaySuperBootstrap.CoinId, amount);
        }
        catch (Exception e)
        {
            // Offline or unauthenticated grants are stored locally by the SDK and retried,
            // so there is nothing to surface to the player.
            Debug.LogWarning("[PlaySuper] coin grant queued locally: " + e.Message);
        }
    }
}
