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
        // Resource key a demo is started with, read from the runtime name
        // first and from the CI name where that is unset. The demo is always
        // given it under the runtime name, the one every language's demo
        // reads first.
        public string DemoResourceKey =>
            RequireFirst(DemoResourceKeyVariable, DemoResourceKeyCiVariable);
        // Cloud endpoint a demo is started with, including the api/v4 path.
        // Read by every language's demo under the same name.
        public string DemoCloudEndpoint => Require(DemoCloudEndpointVariable);

        // The names follow the 51Degrees naming scheme for keys. The runtime
        // name is what a developer sets and is read first. The CI name starts
        // with an underscore, because a shell cannot export a name that
        // starts with a digit, and ends with the product the key must carry,
        // which for these tests is 51Did, because they create identifiers
        // for standard and personalized answers.
        public const string DemoResourceKeyVariable = "51DEGREES_RESOURCE_KEY";
        public const string DemoResourceKeyCiVariable = "_51DEGREES_RESOURCE_KEY_51DID";
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

        // Reads the first of several names that is set, and fails naming all
        // of them, in the order they are read, when none is.
        private static string RequireFirst(params string[] names)
        {
            foreach (var name in names)
            {
                string value = Environment.GetEnvironmentVariable(name);
                if (string.IsNullOrEmpty(value) == false)
                {
                    return value;
                }
            }
            throw new InvalidOperationException(
                "Required environment variable '" +
                string.Join("', or where that is unset '", names) +
                "' is not set.");
        }
    }
}
