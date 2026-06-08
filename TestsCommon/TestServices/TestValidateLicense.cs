namespace FiftyOne.Pipeline.Cloud.Tests.Common.TestServices
{
    // Exposes the license tokens the tests need. Read from the environment so no
    // real license is committed.
    public static class TestValidateLicense
    {
        public static string EnterpriseV4License => TestConfig.Instance().EnterpriseV4License;
    }
}
