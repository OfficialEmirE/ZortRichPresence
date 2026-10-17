using UnityEngine;

namespace ZortRichPresence
{
    internal static class DifficultyScanner
    {
        public static string TryGetDifficulty()
        {
            float chaos = Mathf.Clamp01(WorldServer.ChaosValueNormalized);

            if (chaos < 0.2f)
                return "Normal";
            if (chaos < 0.4f)
                return "Hard";
            if (chaos < 0.6f)
                return "Expert";
            if (chaos < 0.8f)
                return "Unforgiving";

            return "Chaos";
        }
    }
}
