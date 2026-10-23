using System;

namespace SolarMajesty.Tests
{
    /// <summary>
    /// Destructive PlayMode tests (they reset the campaign) run only in the validation project or
    /// in CI. GameCI runs Unity in a container that may not inherit the job's environment, so CI
    /// also passes <c>-smCI</c> on the Unity command line.
    /// </summary>
    public static class ValidationGate
    {
        public static bool IsCi =>
            Environment.GetEnvironmentVariable("CI") == "true" ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-smCI") >= 0;
    }
}
