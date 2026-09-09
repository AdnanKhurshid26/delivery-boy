using PlaySuperUnity;
using UnityEngine;

// Where this game grants PlaySuper coins.
//
// IMPORTANT CONTEXT: Delivery Boy has no spendable in-game currency to mirror 1:1. Its only
// reward is stars (GameData.stars, GamePlayManager.calculateScore returns 1-3), and stars are
// progression, not a wallet - they are deliberately NOT converted into coins. The amount below
// is therefore the studio's own choice, not a mirrored value, and this is the single line to
// change if it should differ.
public static class PlaySuperRewards
{
    public const int LevelCompleteCoins = 100;

    public static void GrantLevelComplete()
    {
        PlaySuperUnitySDK sdk = PlaySuperUnitySDK.Instance;
        if (sdk == null)
        {
            Debug.LogWarning("[PlaySuper] SDK not ready; level-complete coins not granted.");
            return;
        }

        // Fire and forget on purpose: DistributeCoins queues locally when logged out and
        // retries on failure, so it must never gate or block the game's completion flow.
        _ = sdk.DistributeCoins(PlaySuperBootstrap.CoinId, LevelCompleteCoins);
    }
}
