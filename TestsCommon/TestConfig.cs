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
        // Names of the environment variables.
        public const string RootUrlVariable = "CLOUD_ROOT_URL";
        public const string FreeResourceKeyVariable = "FREE_RESOURCE_KEY";
        public const string PaidResourceKeyVariable = "PAID_RESOURCE_KEY";
        public const string EnterpriseV4LicenseVariable = "ENTERPRISE_V4_LICENSE";

        // Base URL of the cloud under test.
        public string RootUrl => Require(RootUrlVariable);
        // Free resource key (no extra properties).
        public string FreeResourceKey => Require(FreeResourceKeyVariable);
        // Paid resource key.
        public string PaidResourceKey => Require(PaidResourceKeyVariable);
        // Enterprise V4 license, passed to the JS endpoint to unlock paid
        // properties.
        public string EnterpriseV4License => Require(EnterpriseV4LicenseVariable);

        // Base URL of the cloud under test, or null when it is not set.
        public string OptionalRootUrl => Optional(RootUrlVariable);
        // Paid resource key, or null when it is not set.
        public string OptionalPaidResourceKey => Optional(PaidResourceKeyVariable);

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
