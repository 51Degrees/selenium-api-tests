using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>Tests for <see cref="SubprocessExampleApp"/>.</summary>
    [TestClass]
    public class SubprocessExampleAppTests
    {
        /// <summary>
        /// A command that exits immediately never serves the readiness URL, so
        /// StartAsync must fail fast and include the captured output.
        /// </summary>
        [TestMethod]
        public async Task StartAsync_ProcessExitsImmediately_ThrowsWithLogs()
        {
            var descriptor = new ExampleDescriptor(
                Lang: "dummy",
                WorkingDir: Environment.CurrentDirectory,
                Command: "dotnet",
                Args: new[] { "--version" },
                ReadinessPath: "/",
                StartupTimeoutSeconds: 10,
                BuildEnv: _ => new Dictionary<string, string>());

            var app = new SubprocessExampleApp(descriptor);
            var options = new ExampleAppOptions(
                Port: TestHelpers.GetRandomUnusedPort(), CloudEndpoint: new Uri("http://localhost:1/"),
                ResourceKey: "x", ExtraEnv: new Dictionary<string, string>());

            InvalidOperationException ex = null;
            try
            {
                await app.StartAsync(options, CancellationToken.None);
            }
            catch (InvalidOperationException e)
            {
                ex = e;
            }
            finally
            {
                await app.DisposeAsync();
            }

            Assert.IsNotNull(ex, "Expected InvalidOperationException but none was thrown.");
            StringAssert.Contains(ex.Message, "exited early");
        }

        /// <summary>A single matching file is returned by its full path.</summary>
        [TestMethod]
        public void ResolveRunArtifact_SingleMatch_ReturnsIt()
        {
            var root = Path.Combine(Path.GetTempPath(), "sea-" + Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(root, "target");
            Directory.CreateDirectory(dir);
            var jar = Path.Combine(dir, "app-1.0-jar-with-dependencies.jar");
            File.WriteAllText(jar, "x");
            try
            {
                var resolved = SubprocessExampleApp.ResolveRunArtifact(
                    root, Path.Combine("target", "*-jar-with-dependencies.jar"));
                Assert.AreEqual(jar, resolved);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        /// <summary>Zero matching files causes a FileNotFoundException.</summary>
        [TestMethod]
        public void ResolveRunArtifact_NoMatch_Throws()
        {
            var root = Path.Combine(Path.GetTempPath(), "sea-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "target"));
            FileNotFoundException ex = null;
            try
            {
                SubprocessExampleApp.ResolveRunArtifact(
                    root, Path.Combine("target", "*-jar-with-dependencies.jar"));
            }
            catch (FileNotFoundException e)
            {
                ex = e;
            }
            finally
            {
                Directory.Delete(root, true);
            }
            Assert.IsNotNull(ex, "Expected FileNotFoundException for zero matches.");
        }

        /// <summary>More than one matching file causes a FileNotFoundException.</summary>
        [TestMethod]
        public void ResolveRunArtifact_MultipleMatches_Throws()
        {
            var root = Path.Combine(Path.GetTempPath(), "sea-" + Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(root, "target");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "a-jar-with-dependencies.jar"), "x");
            File.WriteAllText(Path.Combine(dir, "b-jar-with-dependencies.jar"), "x");
            FileNotFoundException ex = null;
            try
            {
                SubprocessExampleApp.ResolveRunArtifact(
                    root, Path.Combine("target", "*-jar-with-dependencies.jar"));
            }
            catch (FileNotFoundException e)
            {
                ex = e;
            }
            finally
            {
                Directory.Delete(root, true);
            }
            Assert.IsNotNull(ex, "Expected FileNotFoundException for multiple matches.");
        }

        /// <summary>A failing build command makes StartAsync throw before the app is launched.</summary>
        [TestMethod]
        public async Task StartAsync_BuildFails_ThrowsWithBuildOutput()
        {
            var descriptor = new ExampleDescriptor(
                Lang: "dummy",
                WorkingDir: Environment.CurrentDirectory,
                Command: "dotnet",
                Args: new[] { "--version" },
                ReadinessPath: "/",
                StartupTimeoutSeconds: 10,
                BuildEnv: _ => new Dictionary<string, string>(),
                BuildCommand: "dotnet",
                BuildArgs: new[] { "build", "this-project-does-not-exist.csproj" },
                BuildTimeoutSeconds: 120);

            var app = new SubprocessExampleApp(descriptor);
            var options = new ExampleAppOptions(
                Port: TestHelpers.GetRandomUnusedPort(), CloudEndpoint: new Uri("http://localhost:1/"),
                ResourceKey: "x", ExtraEnv: new Dictionary<string, string>());

            InvalidOperationException ex = null;
            try
            {
                await app.StartAsync(options, CancellationToken.None);
            }
            catch (InvalidOperationException e)
            {
                ex = e;
            }
            finally
            {
                await app.DisposeAsync();
            }

            Assert.IsNotNull(ex, "Expected the build failure to throw.");
            StringAssert.Contains(ex.Message, "OUTPUT:");
            StringAssert.Contains(ex.Message, "this-project-does-not-exist");
        }
    }
}
