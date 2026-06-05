namespace FiftyOne.Pipeline.Cloud.Tests.Common.TestElements
{
    /// <summary>
    /// Class contains constant values for test resource keys.
    /// </summary>
    public class TestResourceKey
    {
        public static string FreeResourceKey { get => TestConfig.Instance().FreeResourceKey; }
        public static string FreeResourceKeyProps { get => TestConfig.Instance().FreeResourceKeyProps; }

        public static string PaidResourceKey { get => TestConfig.Instance().PaidResourceKey; }
        public static string PaidResourceKeyProps { get => TestConfig.Instance().PaidResourceKeyProps; }

        public static string ExpiredResourceKey { get => TestConfig.Instance().ExpiredResourceKey; }
        public static string ZeroQuotaResourceKey { get => TestConfig.Instance().ZeroQuotaResourceKey; }
        public static string RestrictedResourceKey { get => TestConfig.Instance().RestrictedResourceKey; }
        public static string DomainRestrictedResourceKey { get => TestConfig.Instance().DomainRestrictedResourceKey; }
        public static string FodidResourceKey { get => TestConfig.Instance().FodidResourceKey; }

        public static string FreeJsonEndpoint { get => FreeResourceKey + ".json"; }
        public static string FreeJsonEndpointProps { get => FreeResourceKeyProps + ".json"; }

        public static string PaidJsonEndpoint { get => PaidResourceKey + ".json"; }
        public static string PaidJsonEndpointProps { get => PaidResourceKeyProps + ".json"; }

        public static string FreeJavaScriptEndpoint { get => FreeResourceKey + ".js"; }
        public static string FreeJavaScriptEndpointProps { get => FreeResourceKeyProps + ".js"; }

        public static string PaidJavaScriptEndpoint { get => PaidResourceKey + ".js"; }
        public static string PaidJavaScriptEndpointProps { get => PaidResourceKeyProps + ".js"; }
    }
}
