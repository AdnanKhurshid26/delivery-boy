using UnityEngine;
using PlaySuperUnity;

namespace DeliveryBoy.PlaySuperIntegration
{
    /// <summary>
    /// Single entry point that boots the PlaySuper SDK for Delivery Boy.
    ///
    /// This runs before the first scene loads, so it beats every Awake(), survives scene
    /// reordering, and needs no GameObject in any scene. The SDK spawns its own
    /// DontDestroyOnLoad "PlaySuper" object internally.
    ///
    /// This is also the ONLY place the api key and coin id are written down -- every other
    /// PlaySuper file in the project reads them from here.
    /// </summary>
    public static class PlaySuperBootstrap
    {
        private const string ApiKey = "ps_test_11a1c19b6c39117688f3d9483481132887ca9cccccc3c7cd7c363cb9f2682e2d";

        /// <summary>The only coin this game may distribute.</summary>
        public const string RewardCoinId = "7b7309f4-ee73-42ab-88ad-a5e9982e4b47";

        private static bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            try
            {
                // isDev omitted -> production environment.
                PlaySuperUnitySDK.Initialize(ApiKey);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PlaySuper] Initialize failed: " + e.Message);
                return;
            }

            // Fire and forget: identity must never block the game from starting.
            _ = PlaySuperIdentity.EnsurePlayerAsync();
        }
    }
}
