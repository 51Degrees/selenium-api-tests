using System;

namespace FiftyOne.Pipeline.Cloud.Tests.Common
{
    // Test environment configuration. Every value comes from an environment
    // variable so nothing sensitive is committed - this repository is public.
    public class TestConfig
    {
        // Base URL of the cloud under test.
        public string RootUrl => Require(_rootUrl);
        // Free resource key (no extra properties).
        public string FreeResourceKey => Require(_freeResourceKey);
        // Paid resource key.
        public string PaidResourceKey => Require(_paidResourceKey);
        // Enterprise V4 license, passed to the JS endpoint to unlock paid properties.
        public string EnterpriseV4License => Require(_enterpriseV4License);
        // Resource key a demo is started with. The same name is read by every
        // language's demo, so the suite hands it over unchanged.
        public string DemoResourceKey => Require(DemoResourceKeyVariable);
        // Cloud endpoint a demo is started with, including the api/v4 path.
        // Read by every language's demo under the same name.
        public string DemoCloudEndpoint => Require(DemoCloudEndpointVariable);

        public const string DemoResourceKeyVariable = "51DEGREES_RESOURCE_KEY";
        public const string DemoCloudEndpointVariable = "51DEGREES_CLOUD_ENDPOINT";
        private const string _rootUrl = "CLOUD_ROOT_URL";
        private const string _freeResourceKey = "FREE_RESOURCE_KEY";
        private const string _paidResourceKey = "PAID_RESOURCE_KEY";
        private const string _enterpriseV4License = "ENTERPRISE_V4_LICENSE";

        private static readonly object _syncLock = new object();
        private static TestConfig _instance;

        private TestConfig() { }

        public static TestConfig Instance()
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    _instance ??= new TestConfig();
                }
            }
            return _instance;
        }

        // Reads a required variable; fails naming the variable if it is missing.
        private static string Require(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    $"Required environment variable '{name}' is not set.");
            }
            return value;
        }
    }
}
