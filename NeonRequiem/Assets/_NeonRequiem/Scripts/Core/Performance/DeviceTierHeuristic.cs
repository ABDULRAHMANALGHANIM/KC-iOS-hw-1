namespace NeonRequiem.Core
{
    /// <summary>
    /// First-run graphics preset suggestion from coarse hardware facts.
    /// <para>
    /// PROVISIONAL: these thresholds are conservative guesses, not measured budgets. They exist so
    /// a first launch on weak hardware does not start on an unsuitable preset, and must be
    /// replaced by a device table once representative phones have been benchmarked. The player
    /// can always override the result in Settings.
    /// </para>
    /// </summary>
    public static class DeviceTierHeuristic
    {
        public const int LowMemoryThresholdMb = 4096;
        public const int LowCoreCountThreshold = 4;

        public static GraphicsPreset SuggestPreset(bool isMobile, int systemMemoryMb, int processorCount)
        {
            if (!isMobile)
            {
                return GraphicsPreset.Quality;
            }

            // Unknown values are reported as 0 or negative on some devices: treat as "no information".
            if (systemMemoryMb > 0 && systemMemoryMb <= LowMemoryThresholdMb)
            {
                return GraphicsPreset.Performance;
            }

            if (processorCount > 0 && processorCount <= LowCoreCountThreshold)
            {
                return GraphicsPreset.Performance;
            }

            // Never auto-select Quality on a phone: sustained thermal load is the player's choice.
            return GraphicsPreset.Balanced;
        }
    }
}
