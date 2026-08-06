using System;
using System.Collections.Generic;
using System.IO;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// Resolves the example app under test from environment variables:
    /// EXAMPLE_URL  -> already running (CI);
    /// EXAMPLE_LANG -> launch that language's example as a subprocess
    ///                 (defaults to "dotnet").
    /// </summary>
    public static class ExampleApps
    {
        private const string ExampleLangVar = "EXAMPLE_LANG";
        private const string ExampleUrlVar = "EXAMPLE_URL";

        /// <summary>The selected language (EXAMPLE_LANG, default "dotnet").</summary>
        public static string SelectedLang =>
            Environment.GetEnvironmentVariable(ExampleLangVar) ?? "dotnet";

        /// <summary>
        /// Path the selected language's client-side script posts refreshed
        /// evidence to. Read from the descriptor rather than the running app,
        /// so it is available for the CI case too, where EXAMPLE_URL points at
        /// an already-running example and no descriptor is used to launch it.
        /// </summary>
        public static string JsonEndpointPath =>
            Descriptors.TryGetValue(SelectedLang, out var descriptor)
                ? descriptor.JsonEndpointPath
                : "/51dpipeline/json";

        /// <summary>
        /// Attempts to create the example provider for the current environment.
        /// Returns false when no descriptor is registered for the selected language,
        /// letting the caller decide whether that should fail the test or skip it.
        /// </summary>
        public static bool TryCreate(out IExampleApp app, out string skipReason)
        {
            var external = Environment.GetEnvironmentVariable(ExampleUrlVar);
            if (!string.IsNullOrEmpty(external))
            {
                app = new ExternalExampleApp(new Uri(external));
                skipReason = null;
                return true;
            }
            if (!Descriptors.TryGetValue(SelectedLang, out var descriptor))
            {
                app = null;
                skipReason =
                    $"No example descriptor registered for EXAMPLE_LANG='{SelectedLang}'. " +
                    $"Known: {string.Join(", ", Descriptors.Keys)}.";
                return false;
            }
            app = new SubprocessExampleApp(descriptor);
            skipReason = null;
            return true;
        }

        /// <summary>Per-language launch descriptors.</summary>
        public static readonly IReadOnlyDictionary<string, ExampleDescriptor> Descriptors =
            new Dictionary<string, ExampleDescriptor>
            {
                ["dotnet"] = new ExampleDescriptor(
                    Lang: "dotnet",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "device-detection-dotnet-examples",
                        "Examples", "Cloud", "GettingStarted-Web"),
                    Command: "dotnet",
                    Args: new[] { "run", "-c", "Release", "--no-launch-profile" },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 180,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        ["SUPER_RESOURCE_KEY"] = o.ResourceKey,
                        // the endpoint must include the api/v4 path
                        ["FIFTYONE_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["ASPNETCORE_URLS"] = $"http://localhost:{o.Port}",
                        // run on a newer .NET runtime if the exact one isn't installed
                        ["DOTNET_ROLL_FORWARD"] = "Major",
                    }),
                ["java"] = new ExampleDescriptor(
                    Lang: "java",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "device-detection-java-examples"),
                    Command: "java",
                    Args: new[] { "-jar" },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 120,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        ["PORT"] = $"{o.Port}",
                        ["TestCloudEndpoint"] = new Uri(o.CloudEndpoint, "api/v4").ToString(),
                        ["TestResourceKey"] = o.ResourceKey,
                    },
                    BuildCommand: "mvn",
                    BuildArgs: new[] { "-pl", "web/getting-started.cloud", "-am", "package", "-DskipTests" },
                    RunArtifactGlob: "web/getting-started.cloud/target/*-jar-with-dependencies.jar",
                    BuildTimeoutSeconds: 600,
                    JsonEndpointPath: "/51Degrees.core.json"),
                ["node"] = new ExampleDescriptor(
                    Lang: "node",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "device-detection-node",
                        "fiftyone.devicedetection.cloud",
                        "examples", "cloud", "gettingstarted-web"),
                    Command: "node",
                    Args: new[] { "gettingStarted.js" },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 60,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        ["PORT"] = $"{o.Port}",
                        // the engine reads the cloud endpoint from FOD_CLOUD_API_URL,
                        // the example reads the key from RESOURCE_KEY
                        ["FOD_CLOUD_API_URL"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["RESOURCE_KEY"] = o.ResourceKey,
                    },
                    BuildCommand: "npm",
                    // package.json lives one level up from examples/cloud/gettingstarted-web
                    BuildArgs: new[] { "install", "--prefix", "../../.." },
                    BuildTimeoutSeconds: 300,
                    JsonEndpointPath: "/json"),
                ["python"] = new ExampleDescriptor(
                    Lang: "python",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "device-detection-python",
                        "fiftyone_devicedetection_examples"),
                    // the venv's python (absolute path)
                    Command: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "device-detection-python",
                        "fiftyone_devicedetection_examples",
                        ".venv", "bin", "python"),
                    Args: new[] { "-m", "fiftyone_devicedetection_examples.cloud.gettingstarted_web" },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 60,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        ["PORT"] = $"{o.Port}",
                        ["cloud_endpoint"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["resource_key"] = o.ResourceKey,
                    },
                    BuildCommand: "bash",
                    BuildArgs: new[] { "-c", "python3 -m venv .venv && .venv/bin/pip install -e ." },
                    BuildTimeoutSeconds: 600,
                    JsonEndpointPath: "/json"),
                ["php"] = new ExampleDescriptor(
                    Lang: "php",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "device-detection-php", "examples"),
                    Command: "bash",
                    // clear the cached pipeline so the endpoint override takes effect
                    Args: new[]
                    {
                        "-c",
                        "rm -f cloud/classesgettingStartedWeb.pipeline cloud/classes/gettingStartedWeb.pipeline; " +
                        "exec php -S localhost:$PORT cloud/gettingStartedWeb.php",
                    },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 60,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        ["PORT"] = $"{o.Port}",
                        ["cloud_endpoint"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["resource_key"] = o.ResourceKey,
                    },
                    BuildCommand: "composer",
                    BuildArgs: new[] { "install", "--working-dir=.." },
                    BuildTimeoutSeconds: 600,
                    JsonEndpointPath: "/json"),
                ["rust"] = new ExampleDescriptor(
                    Lang: "rust",
                    // the examples form their own workspace under examples/;
                    // --config source.toml patches the fiftyone-* dependencies to
                    // the checked-out source tree instead of crates.io
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "rust", "examples"),
                    Command: "cargo",
                    Args: new[]
                    {
                        "run", "--config", "source.toml",
                        "-p", "device-detection-examples",
                        "--bin", "dd-web-getting-started-cloud",
                    },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 60,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        ["PORT"] = $"{o.Port}",
                        // the engine reads the base cloud URL (which includes the
                        // api/v4 path) from 51DEGREES_CLOUD_ENDPOINT
                        ["51DEGREES_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["51DEGREES_RESOURCE_KEY"] = o.ResourceKey,
                    },
                    // compile ahead of `cargo run` so the native-code build gets
                    // the build timeout, leaving startup to the run timeout
                    BuildCommand: "cargo",
                    BuildArgs: new[]
                    {
                        "build", "--config", "source.toml",
                        "-p", "device-detection-examples",
                        "--bin", "dd-web-getting-started-cloud",
                    },
                    BuildTimeoutSeconds: 900),
            };
    }
}
