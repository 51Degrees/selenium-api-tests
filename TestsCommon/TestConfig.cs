using System;

namespace FiftyOne.Pipeline.Cloud.Tests.Common
{
    // Test environment configuration. Every value comes from an environment
    // variable so nothing sensitive is committed - this repository is public.
    //
    // Nothing is read until a test asks for it, so a missing variable only
    // fails the tests that need it.
    public class TestConfig
    {
        // Names of the environment variables. Each setting is read under
        // one _51DEGREES_ name.
        //
        // The cloud under test, including its api/v4 path.
        public const string CloudEndpointVariable = "_51DEGREES_CLOUD_ENDPOINT";
        // A resource key for the free properties.
        public const string FreeResourceKeyVariable = "_51DEGREES_RESOURCE_KEY_FREE";
        // A resource key with every property.
        public const string PaidResourceKeyVariable = "_51DEGREES_RESOURCE_KEY_BESPOKE";
        // A license key, passed to the JavaScript endpoint to unlock paid
        // properties.
        public const string LicenseKeyVariable = "_51DEGREES_LICENSE_KEY_BESPOKE";
        // The resource key a demo is started with. The demos create 51Dids,
        // so this is the key for the 51Did service.
        public const string DemoResourceKeyVariable = "_51DEGREES_RESOURCE_KEY_51DID";

        // The public cloud service, used when no endpoint is set.
        public const string PublicCloudEndpoint = "https://cloud.51degrees.com/api/v4/";

        private const string ApiPath = "/api/v4";

        private static readonly object _syncLock = new object();
        private static TestConfig _instance;

        private readonly Func<string, string> _getVariable;

        // Reads from the given lookup instead of the process environment, so
        // the rules can be tested without changing the environment of the
        // whole test run.
        public TestConfig(Func<string, string> getVariable)
        {
            _getVariable = getVariable
                ?? throw new ArgumentNullException(nameof(getVariable));
        }

        public static TestConfig Instance()
        {
            if (_instance == null)
            {
                lock (_syncLock)
                {
                    _instance ??= new TestConfig(
                        Environment.GetEnvironmentVariable);
                }
            }
            return _instance;
        }

        // Root URL of the cloud under test, being the endpoint without its
        // api/v4 path, or the public service's when no endpoint is set.
        public string RootUrl => RootOf(DemoCloudEndpoint);
        // Root URL of the cloud under test, or null when no endpoint is set.
        public string OptionalRootUrl
        {
            get
            {
                var endpoint = Optional(CloudEndpointVariable);
                return endpoint == null ? null : RootOf(endpoint);
            }
        }
        // Free resource key (no extra properties).
        public string FreeResourceKey => Require(FreeResourceKeyVariable);
        // Paid resource key.
        public string PaidResourceKey => Require(PaidResourceKeyVariable);
        // Paid resource key, or null when it is not set.
        public string OptionalPaidResourceKey => Optional(PaidResourceKeyVariable);
        // License passed to the JavaScript endpoint to unlock paid
        // properties.
        public string EnterpriseV4License => Require(LicenseKeyVariable);

        // Resource key a demo is started with.
        public string DemoResourceKey => Require(DemoResourceKeyVariable);
        // Cloud endpoint a demo is started with, including the api/v4 path.
        public string DemoCloudEndpoint =>
            Optional(CloudEndpointVariable) ?? PublicCloudEndpoint;

        // The endpoint without its api/v4 path, ending with a slash.
        private static string RootOf(string endpoint)
        {
            var root = endpoint.TrimEnd('/');
            if (root.EndsWith(ApiPath, StringComparison.OrdinalIgnoreCase))
            {
                root = root.Substring(0, root.Length - ApiPath.Length);
            }
            return root + "/";
        }

        // Reads a variable, returning null when it is missing or empty.
        public string Optional(string name)
        {
            var value = _getVariable(name);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        // Reads a required variable and fails naming the variable if it is
        // missing. The value itself is never part of the message.
        public string Require(string name)
        {
            return Optional(name) ?? throw new InvalidOperationException(
                $"Required environment variable '{name}' is not set.");
        }
    }
}
