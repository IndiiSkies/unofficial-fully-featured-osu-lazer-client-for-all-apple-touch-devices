// Compatibility forms of osu!lazer scoring interfaces. See ThirdParty/osu-LICENCE.
using System;

namespace osu.Game.Beatmaps
{
    public struct DifficultyRange
    {
        public readonly double Min, Mid, Max;
        public DifficultyRange(double min, double mid, double max) { Min = min; Mid = mid; Max = max; }
    }

    public static class LegacyDifficulty
    {
        // From IBeatmapDifficultyInfo.DifficultyRange in the supplied lazer source.
        public static double DifficultyRange(double difficulty, DifficultyRange range)
        {
            if (difficulty > 5) return range.Mid + (range.Max - range.Mid) * (difficulty - 5) / 5;
            if (difficulty < 5) return range.Mid + (range.Mid - range.Min) * (difficulty - 5) / 5;
            return range.Mid;
        }
    }
}

namespace osu.Game.Rulesets.Scoring
{
    // Preserve upstream's relative ordering for ResultFor's search.
    public enum HitResult { None, Miss, Meh, Ok, Good, Great, Perfect }

    public abstract class HitWindows
    {
        public abstract bool IsHitResultAllowed(HitResult result);
        public abstract void SetDifficulty(double difficulty);
        public abstract double WindowFor(HitResult result);

        // Equivalent to upstream HitWindows.ResultFor; no tuples or framework dependency.
        public HitResult ResultFor(double timeOffset)
        {
            timeOffset = Math.Abs(timeOffset);
            for (HitResult result = HitResult.Perfect; result >= HitResult.Miss; result--)
                if (IsHitResultAllowed(result) && timeOffset <= WindowFor(result)) return result;
            return HitResult.None;
        }
    }
}
