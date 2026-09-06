namespace SolarMajesty
{
    /// <summary>
    /// Editor still shutter only. <c>CaptureStill</c> arms this so Phase 4 campus frames
    /// cannot trip OUTPOST LOST (colony-extinct / life-support fail) or Mars-descent
    /// toast / cutscene overlays over the kits. Never arm from play — not god-mode.
    /// </summary>
    public static class StillCaptureHold
    {
        public static bool Active { get; private set; }

        public static void Arm() => Active = true;

        public static void Disarm() => Active = false;
    }
}
