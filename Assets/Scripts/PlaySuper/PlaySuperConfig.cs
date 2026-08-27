// PlaySuper integration - shared configuration.
//
// This is the ONLY place the API key and coin id live. Every other PlaySuper
// call site reads from here, so swapping environments is a single edit.
//
// NOTE: changing ApiKey is an IDENTITY change, not a one-line edit. The stored
// player uuid is bound to the key in PlaySuperBootstrap - keep that binding or
// the store will open unauthenticated after the swap.
public static class PlaySuperConfig
{
    /// <summary>Live PRODUCTION key for "Agentic Game".</summary>
    public const string ApiKey = "ps_live_8912e8ce5b84fc14c13f611b1b030ed3dbd5b1a40f1a075713454983cbdd1495";

    /// <summary>The only coin this game has in PlaySuper.</summary>
    public const string RewardCoinId = "8f702988-bc8c-4448-b81a-58c327f822b7";

    /// <summary>
    /// Coins granted for clearing a level.
    ///
    /// Delivery Boy has no in-game spendable currency to mirror - stars and level
    /// progress are progression, not a wallet - so this amount was chosen by the
    /// studio rather than derived from existing game code.
    /// </summary>
    public const int LevelCompleteReward = 100;
}
