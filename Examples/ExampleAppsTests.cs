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
    }
}
