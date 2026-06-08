namespace FiftyOne.Pipeline.Cloud.Tests.Common.TestElements
{
    // Resource keys and the JS endpoints derived from them.
    public class TestResourceKey
    {
        public static string FreeResourceKey => TestConfig.Instance().FreeResourceKey;
        public static string PaidResourceKey => TestConfig.Instance().PaidResourceKey;

        public static string FreeJavaScriptEndpoint => FreeResourceKey + ".js";
        public static string PaidJavaScriptEndpoint => PaidResourceKey + ".js";
    }
}
