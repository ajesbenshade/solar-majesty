namespace SolarMajesty
{
    /// <summary>
    /// Identical overseer lines inside this window are one notice. A purse nibble that
    /// fires three times in a frame should toast once.
    /// </summary>
    public static class NoticeDedupe
    {
        public const float WindowSeconds = 2.5f;

        public static bool IsRepeat(string previous, float previousAt, string next, float now, float windowSeconds)
        {
            return !string.IsNullOrEmpty(next)
                && next == previous
                && now - previousAt < windowSeconds;
        }
    }
}
