namespace UnityEngine.TestTools
{
    [System.AttributeUsage(System.AttributeTargets.Method)] public class UnityTestAttribute : System.Attribute { }
    public static class LogAssert { public static bool ignoreFailingMessages; public static void Expect(UnityEngine.LogType t, string m) { } public static void Expect(UnityEngine.LogType t, System.Text.RegularExpressions.Regex r) { } public static void NoUnexpectedReceived() { } }
}
