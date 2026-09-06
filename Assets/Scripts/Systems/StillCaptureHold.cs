namespace SolarMajesty
{
    /// <summary>
    /// Editor still shutter only. <c>CaptureStill</c> arms this so Phase 4 campus frames
    /// cannot trip OUTPOST LOST (colony-extinct / life-support fail) or W2 cutscene /
    /// ArrivalLog / VictoryLog narrative modals over the kits.
    /// Never arm from play — not god-mode.
    /// </summary>
    public static class StillCaptureHold
    {
        /// <summary>
        /// Editor SessionState key. Survives domain reload and Enter Play Mode static reset
        /// so <c>GameLoop</c> can re-arm before <c>BeginNarrativeSession</c>.
        /// </summary>
        public const string EditorSessionKey = "SM_CaptureStill_Hold";

        public static bool Active { get; private set; }

        public static void Arm() => Active = true;

        public static void Disarm() => Active = false;
    }
}
