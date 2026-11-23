using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>Tests for <see cref="ExampleApps"/> descriptors.</summary>
    [TestClass]
    public class ExampleAppsTests
    {
        /// <summary>The java example is built with Maven and run from a jar.</summary>
        [TestMethod]
        public void Descriptors_Java_BuildsWithMavenAndRunsJar()
        {
            Assert.IsTrue(ExampleApps.Descriptors.ContainsKey("java"),
                "java descriptor should be registered");
            var java = ExampleApps.Descriptors["java"];
            Assert.AreEqual("mvn", java.BuildCommand);
            Assert.AreEqual("java", java.Command);
            StringAssert.Contains(java.RunArtifactGlob, "jar-with-dependencies");
        }

        /// <summary>The node example is installed with npm and run with node.</summary>
        [TestMethod]
        public void Descriptors_Node_BuildsWithNpmAndRunsNode()
        {
            Assert.IsTrue(ExampleApps.Descriptors.ContainsKey("node"),
                "node descriptor should be registered");
            var node = ExampleApps.Descriptors["node"];
            Assert.AreEqual("npm", node.BuildCommand);
            Assert.AreEqual("node", node.Command);
            Assert.IsNull(node.RunArtifactGlob);
            StringAssert.Contains(node.Args[0], "gettingStarted.js");
        }

        /// <summary>The python example is installed in a venv and run with python.</summary>
        [TestMethod]
        public void Descriptors_Python_BuildsWithVenvAndRunsPython()
        {
            Assert.IsTrue(ExampleApps.Descriptors.ContainsKey("python"),
                "python descriptor should be registered");
            var python = ExampleApps.Descriptors["python"];
            Assert.AreEqual("bash", python.BuildCommand);
            Assert.IsNull(python.RunArtifactGlob);
            StringAssert.Contains(python.Command, ".venv");
            StringAssert.Contains(python.Args[1], "gettingstarted_web");
        }

        /// <summary>The php example is installed with composer and run with php -S.</summary>
        [TestMethod]
        public void Descriptors_Php_BuildsWithComposerAndRunsPhp()
        {
            Assert.IsTrue(ExampleApps.Descriptors.ContainsKey("php"),
                "php descriptor should be registered");
            var php = ExampleApps.Descriptors["php"];
            Assert.AreEqual("composer", php.BuildCommand);
            Assert.AreEqual("bash", php.Command);
            Assert.IsNull(php.RunArtifactGlob);
            StringAssert.Contains(php.Args[1], "php -S");
        }

        /// <summary>The rust example is built and run with cargo against local source.</summary>
        [TestMethod]
        public void Descriptors_Rust_BuildsAndRunsWithCargo()
        {
            Assert.IsTrue(ExampleApps.Descriptors.ContainsKey("rust"),
                "rust descriptor should be registered");
            var rust = ExampleApps.Descriptors["rust"];
            Assert.AreEqual("cargo", rust.BuildCommand);
            Assert.AreEqual("cargo", rust.Command);
            Assert.IsNull(rust.RunArtifactGlob);
            // the examples workspace must be patched to the checked-out source
            Assert.IsTrue(rust.Args.Contains("source.toml"),
                "run args should patch dependencies to the local source tree");
            Assert.IsTrue(rust.Args.Contains("dd-web-getting-started-cloud"),
                "run args should target the cloud getting-started web binary");
        }

        private static IDictionary<string, string> EnvFor(
            ExampleDescriptor descriptor) =>
            descriptor.BuildEnv(new ExampleAppOptions(
                Port: 5123,
                CloudEndpoint: new Uri("https://cloud.example.test/"),
                ResourceKey: "key",
                ExtraEnv: new Dictionary<string, string>()));

        /// <summary>
        /// Each example is handed the key and the endpoint, with its api/v4
        /// path, under the _51DEGREES_ names every language reads.
        /// </summary>
        [TestMethod]
        [DataRow("dotnet")]
        [DataRow("java")]
        [DataRow("node")]
        [DataRow("python")]
        [DataRow("php")]
        [DataRow("rust")]
        public void Descriptors_SetTheKeyAndEndpointNames(string lang)
        {
            var env = EnvFor(ExampleApps.Descriptors[lang]);
            Assert.AreEqual("key", env["_51DEGREES_RESOURCE_KEY"]);
            Assert.AreEqual(
                "https://cloud.example.test/api/v4/",
                env["_51DEGREES_CLOUD_ENDPOINT"]);
        }

        /// <summary>
        /// The examples that choose their own port are given it as
        /// _51DEGREES_EXAMPLE_HTTP_PORTS. The .NET example is given it the
        /// way ASP.NET Core takes it.
        /// </summary>
        [TestMethod]
        [DataRow("java")]
        [DataRow("node")]
        [DataRow("python")]
        [DataRow("php")]
        [DataRow("rust")]
        public void Descriptors_SetThePortName(string lang)
        {
            var env = EnvFor(ExampleApps.Descriptors[lang]);
            Assert.AreEqual("5123", env["_51DEGREES_EXAMPLE_HTTP_PORTS"]);
        }

        [TestMethod]
        public void Descriptors_Php_ListensOnThePortName()
        {
            StringAssert.Contains(
                ExampleApps.Descriptors["php"].Args[1],
                "localhost:$_51DEGREES_EXAMPLE_HTTP_PORTS");
        }

        [TestMethod]
        public void Demos_Dotnet_SetsTheKeyAndEndpointNames()
        {
            var env = EnvFor(ExampleApps.Demos["dotnet"]);
            Assert.AreEqual("key", env["_51DEGREES_RESOURCE_KEY"]);
            Assert.AreEqual(
                "https://cloud.example.test/api/v4/",
                env["_51DEGREES_CLOUD_ENDPOINT"]);
        }
    }
}
