using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FiftyOne.Pipeline.Cloud.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// What the suite demands from the environment, by case. The rules are
    /// read through a lookup the test supplies, so nothing here changes the
    /// environment of the run around it.
    /// </summary>
    [TestClass]
    public class ContractSettingsTests
    {
        private const string ExampleUrl = "http://localhost:3000/";
        private const string CloudUrl = "https://cloud.51degrees.com/";

        private static TestConfig Config(params string[] namesAndValues)
        {
            var values = new Dictionary<string, string>();
            for (var i = 0; i < namesAndValues.Length; i += 2)
            {
                values[namesAndValues[i]] = namesAndValues[i + 1];
            }
            return new TestConfig(
                name => values.TryGetValue(name, out var value) ? value : null);
        }

        /// <summary>
        /// The case an on-premise example in CI is in: the example is already
        /// running and there is no cloud and no key. The suite must ask for
        /// neither.
        /// </summary>
        [TestMethod]
        public void BuildOptions_RunningExample_NeedsNoCloudUrlAndNoKey()
        {
            var app = new ExternalExampleApp(new Uri(ExampleUrl));

            var options = ExampleApps.BuildOptions(app, 1234, Config());

            Assert.AreEqual(1234, options.Port);
            Assert.IsNull(options.CloudEndpoint,
                "no cloud endpoint should be invented when none is configured");
            Assert.IsNull(options.ResourceKey,
                "no resource key should be demanded for a running example");
        }

        /// <summary>
        /// Set for a running example, both values are still passed on, so a
        /// cloud example started by CI is unaffected.
        /// </summary>
        [TestMethod]
        public void BuildOptions_RunningExample_PassesOnWhatIsSet()
        {
            var app = new ExternalExampleApp(new Uri(ExampleUrl));

            var options = ExampleApps.BuildOptions(app, 1234, Config(
                TestConfig.RootUrlVariable, CloudUrl,
                TestConfig.PaidResourceKeyVariable, "a-key"));

            Assert.AreEqual(new Uri(CloudUrl), options.CloudEndpoint);
            Assert.AreEqual("a-key", options.ResourceKey);
        }

        /// <summary>
        /// An example this suite launches has to be told where the cloud is,
        /// so a missing CLOUD_ROOT_URL fails and says which variable to set.
        /// </summary>
        [TestMethod]
        public void BuildOptions_LaunchedExample_RequiresTheCloudUrl()
        {
            var app = new SubprocessExampleApp(ExampleApps.Descriptors["dotnet"]);

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => ExampleApps.BuildOptions(app, 1234, Config(
                    TestConfig.PaidResourceKeyVariable, "a-key")));

            StringAssert.Contains(ex.Message, TestConfig.RootUrlVariable);
        }

        /// <summary>
        /// The same for the key, which the example needs to call the cloud.
        /// </summary>
        [TestMethod]
        public void BuildOptions_LaunchedExample_RequiresTheResourceKey()
        {
            var app = new SubprocessExampleApp(ExampleApps.Descriptors["dotnet"]);

            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => ExampleApps.BuildOptions(app, 1234, Config(
                    TestConfig.RootUrlVariable, CloudUrl)));

            StringAssert.Contains(ex.Message, TestConfig.PaidResourceKeyVariable);
        }

        /// <summary>
        /// EXAMPLE_URL selects the already-running example whatever
        /// EXAMPLE_LANG says.
        /// </summary>
        [TestMethod]
        public void TryCreate_ExampleUrlSet_UsesTheRunningExample()
        {
            var values = new Dictionary<string, string>
            {
                ["EXAMPLE_URL"] = ExampleUrl,
                ["EXAMPLE_LANG"] = "rust",
            };

            var created = ExampleApps.TryCreate(
                name => values.TryGetValue(name, out var value) ? value : null,
                out var app,
                out var skipReason);

            Assert.IsTrue(created, skipReason);
            Assert.IsInstanceOfType<ExternalExampleApp>(app);
            Assert.AreEqual(new Uri(ExampleUrl), app.BaseUrl);
        }

        /// <summary>
        /// A language with no descriptor is skipped rather than failed, and
        /// the reason says what was asked for and what is known.
        /// </summary>
        [TestMethod]
        public void TryCreate_UnknownLanguage_ExplainsItself()
        {
            var created = ExampleApps.TryCreate(
                name => name == "EXAMPLE_LANG" ? "cobol" : null,
                out _,
                out var skipReason);

            Assert.IsFalse(created);
            StringAssert.Contains(skipReason, "cobol");
            StringAssert.Contains(skipReason, "dotnet");
        }

        /// <summary>
        /// A missing value is reported by name and the value itself is never
        /// part of a message.
        /// </summary>
        [TestMethod]
        public void TestConfig_MissingValue_NamesTheVariable()
        {
            var config = Config();

            Assert.IsNull(config.Optional(TestConfig.RootUrlVariable));
            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => config.Require(TestConfig.RootUrlVariable));
            StringAssert.Contains(ex.Message, TestConfig.RootUrlVariable);
        }

        /// <summary>
        /// An empty value counts as missing, which is what a CI script that
        /// exports an unset variable produces.
        /// </summary>
        [TestMethod]
        public void TestConfig_EmptyValue_CountsAsMissing()
        {
            var config = Config(TestConfig.RootUrlVariable, "");

            Assert.IsNull(config.Optional(TestConfig.RootUrlVariable));
        }

        /// <summary>
        /// The python example is launched through the interpreter inside its
        /// virtual environment, which is in a different place on Windows.
        /// </summary>
        [TestMethod]
        public void VenvPython_IsTheInterpreterForThisOperatingSystem()
        {
            var python = ExampleApps.VenvPython(".venv");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.AreEqual(
                    Path.Combine(".venv", "Scripts", "python.exe"), python);
            }
            else
            {
                Assert.AreEqual(Path.Combine(".venv", "bin", "python"), python);
            }
        }
    }
}
