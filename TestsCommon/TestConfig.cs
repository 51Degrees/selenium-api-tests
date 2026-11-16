using System;
using System.Collections.Generic;
using System.Linq;

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

        // Resource key a demo is started with. The runtime name is read
        // first, then the CI name, and only where neither is set the older
        // name, which is reported as deprecated when it supplies the key.
        // This is the order the demos read their own names in.
        public string DemoResourceKey =>
            RequireCurrentOrOld(
                DemoResourceKeyOldVariable,
                DemoResourceKeyVariable,
                DemoResourceKeyCiVariable);
        // Cloud endpoint a demo is started with, including the api/v4 path,
        // from the runtime name or, where that is unset, the older name.
        public string DemoCloudEndpoint =>
            RequireCurrentOrOld(
                DemoCloudEndpointOldVariable, DemoCloudEndpointVariable);

        // The names follow the 51Degrees naming scheme. Each starts with an
        // underscore, because a shell cannot export a name that starts with
        // a digit. The CI name ends with the product the key must carry,
        // which for these tests is 51Did, because they create identifiers
        // for standard and personalized answers. The older names are still
        // read, after every current name.
        public const string DemoResourceKeyVariable = "_51DEGREES_RESOURCE_KEY";
        public const string DemoResourceKeyCiVariable = "_51DEGREES_RESOURCE_KEY_51DID";
        public const string DemoResourceKeyOldVariable = "51DEGREES_RESOURCE_KEY";
        public const string DemoCloudEndpointVariable = "_51DEGREES_CLOUD_ENDPOINT";
        public const string DemoCloudEndpointOldVariable = "51DEGREES_CLOUD_ENDPOINT";

        private static readonly object _syncLock = new object();
        private static TestConfig _instance;

        private static readonly HashSet<string> _reported = new HashSet<string>();

        private readonly Func<string, string> _getVariable;
        private readonly Action<string> _reportOldName;

        // Reads from the given lookup instead of the process environment, so
        // the rules can be tested without changing the environment of the
        // whole test run.
        public TestConfig(Func<string, string> getVariable)
            : this(getVariable, ReportOnceToErrorStream)
        {
        }

        // As above, with where the sentence about a deprecated name goes, so
        // that sentence can be tested too.
        public TestConfig(
            Func<string, string> getVariable, Action<string> reportOldName)
        {
            _getVariable = getVariable
                ?? throw new ArgumentNullException(nameof(getVariable));
            _reportOldName = reportOldName
                ?? throw new ArgumentNullException(nameof(reportOldName));
        }

        // Each deprecated name is reported once in a process, on the error
        // stream, however many tests read it.
        private static void ReportOnceToErrorStream(string message)
        {
            lock (_reported)
            {
                if (_reported.Add(message) == false)
                {
                    return;
                }
            }
            Console.Error.WriteLine(message);
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

        // Reads the first of the current names that is set and, only where
        // none is, the older name they replace, saying in one sentence that
        // it is deprecated and which name replaces it. Fails naming every
        // name, in the order they are read, when none is set. Read through
        // Optional, so a TestConfig given its own lookup uses it here too.
        public string RequireCurrentOrOld(
            string oldName, params string[] names)
        {
            foreach (var name in names)
            {
                var value = Optional(name);
                if (value != null)
                {
                    return value;
                }
            }
            var old = Optional(oldName);
            if (old != null)
            {
                _reportOldName(
                    $"The environment variable {oldName} is deprecated and "
                    + $"{names[0]} replaces it. Set {names[0]} to the same "
                    + "value instead.");
                return old;
            }
            throw new InvalidOperationException(
                "Required environment variable '" +
                string.Join(
                    "', or where that is unset '",
                    names.Append(oldName)) +
                "' is not set.");
        }
    }
}
