using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using FiftyOne.Pipeline.Cloud.Tests.Common;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// Resolves the example app under test from environment variables:
    /// _51DEGREES_EXAMPLE_URL  -> already running (CI);
    /// _51DEGREES_EXAMPLE_LANG -> launch that language's example as a
    ///                            subprocess (defaults to "dotnet").
    /// </summary>
    public static class ExampleApps
    {
        private const string ExampleLangVar = "_51DEGREES_EXAMPLE_LANG";

        /// <summary>
        /// Names the address of an example that is already running.
        /// </summary>
        public const string ExampleUrlVar = "_51DEGREES_EXAMPLE_URL";

        /// <summary>
        /// Names the port an example the suite starts listens on.
        /// </summary>
        private const string PortVar = "_51DEGREES_EXAMPLE_HTTP_PORTS";

        /// <summary>
        /// The selected language (_51DEGREES_EXAMPLE_LANG, default "dotnet").
        /// </summary>
        public static string SelectedLang =>
            Environment.GetEnvironmentVariable(ExampleLangVar) ?? "dotnet";

        /// <summary>
        /// Path the selected language's client-side script posts refreshed
        /// evidence to. Read from the descriptor rather than the running app,
        /// so it is available for the CI case too, where _51DEGREES_EXAMPLE_URL points at
        /// an already-running example and no descriptor is used to launch it.
        /// </summary>
        public static string JsonEndpointPath =>
            Descriptors.TryGetValue(SelectedLang, out var descriptor)
                ? descriptor.JsonEndpointPath
                : "/51dpipeline/json";

        /// <summary>
        /// Every path the selected language's client-side script may post to,
        /// being <see cref="JsonEndpointPath"/> and any older path the
        /// language still answers.
        /// </summary>
        public static IReadOnlyList<string> JsonEndpointPaths
        {
            get
            {
                var paths = new List<string> { JsonEndpointPath };
                if (Descriptors.TryGetValue(SelectedLang, out var descriptor)
                    && descriptor.AlternativeJsonEndpointPaths != null)
                {
                    paths.AddRange(descriptor.AlternativeJsonEndpointPaths);
                }
                return paths;
            }
        }

        /// <summary>
        /// Element the selected language's page renders its client-side results
        /// into. Read from the descriptor so it is available for the CI case
        /// too, where _51DEGREES_EXAMPLE_URL points at an already-running
        /// example.
        /// </summary>
        public static string ClientResultsElementId =>
            Descriptors.TryGetValue(SelectedLang, out var descriptor)
                ? descriptor.ClientResultsElementId
                : "content";

        /// <summary>
        /// Attempts to create the example provider for the current environment.
        /// Returns false when no descriptor is registered for the selected language,
        /// letting the caller decide whether that should fail the test or skip it.
        /// </summary>
        public static bool TryCreate(out IExampleApp app, out string skipReason) =>
            TryCreate(Environment.GetEnvironmentVariable, out app, out skipReason);

        /// <summary>
        /// As <see cref="TryCreate(out IExampleApp, out string)"/>, reading
        /// the variables through <paramref name="getVariable"/> so the rules
        /// can be tested without changing the process environment.
        /// </summary>
        public static bool TryCreate(
            Func<string, string> getVariable,
            out IExampleApp app,
            out string skipReason)
        {
            var external = getVariable(ExampleUrlVar);
            if (!string.IsNullOrEmpty(external))
            {
                app = new ExternalExampleApp(new Uri(external));
                skipReason = null;
                return true;
            }
            var lang = getVariable(ExampleLangVar) ?? "dotnet";
            if (!Descriptors.TryGetValue(lang, out var descriptor))
            {
                app = null;
                skipReason =
                    $"No example descriptor registered for {ExampleLangVar}='{lang}'. " +
                    $"Known: {string.Join(", ", Descriptors.Keys)}.";
                return false;
            }
            app = new SubprocessExampleApp(descriptor);
            skipReason = null;
            return true;
        }

        private const string DemoLangVar = "_51DEGREES_DEMO_LANG";
        private const string DemoUrlVar = "_51DEGREES_DEMO_URL";

        /// <summary>
        /// The selected demo's language (_51DEGREES_DEMO_LANG, default
        /// "dotnet").
        /// </summary>
        /// <remarks>
        /// A demo is chosen separately from the example the Contract tests
        /// use, because the two are different apps. The GettingStarted-Web
        /// examples prove a web integration works, and a demo carries every
        /// page the Browser51Did tests drive, which each language mirrors so
        /// that the same tests hold for all of them. _51DEGREES_DEMO_URL and
        /// _51DEGREES_DEMO_LANG work the way _51DEGREES_EXAMPLE_URL and
        /// _51DEGREES_EXAMPLE_LANG do.
        /// </remarks>
        public static string SelectedDemoLang =>
            Environment.GetEnvironmentVariable(DemoLangVar) ?? "dotnet";

        /// <summary>
        /// Attempts to create the demo for the current environment, being the
        /// one already running at _51DEGREES_DEMO_URL, or else the
        /// _51DEGREES_DEMO_LANG demo
        /// launched from its sibling checkout. Returns false when no demo is
        /// registered for that language.
        /// </summary>
        public static bool TryCreateDemo(
            out IExampleApp app,
            out ExampleDescriptor descriptor,
            out string skipReason)
        {
            Demos.TryGetValue(SelectedDemoLang, out descriptor);
            var external = Environment.GetEnvironmentVariable(DemoUrlVar);
            if (!string.IsNullOrEmpty(external))
            {
                app = new ExternalExampleApp(new Uri(external));
                skipReason = null;
                return true;
            }
            if (descriptor == null)
            {
                app = null;
                skipReason =
                    $"No demo registered for {DemoLangVar}='{SelectedDemoLang}'. " +
                    $"Known: {string.Join(", ", Demos.Keys)}.";
                return false;
            }
            app = new SubprocessExampleApp(descriptor);
            skipReason = null;
            return true;
        }

        /// <summary>
        /// Per-language demos. Each serves the same pages under the same
        /// routes, and reads its input data from a resource key and a cloud
        /// endpoint variable, so a demo in another language is added here and
        /// nothing else in the suite changes.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, ExampleDescriptor> Demos =
            new Dictionary<string, ExampleDescriptor>
            {
                ["dotnet"] = new ExampleDescriptor(
                    Lang: "dotnet",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "51degrees-dotnet", "examples", "device-detection",
                        "Cloud", "pmp-web"),
                    Command: "dotnet",
                    Args: new[] { "run", "-c", "Release", "--no-launch-profile" },
                    ReadinessPath: "/cloud/common",
                    StartupTimeoutSeconds: 180,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        ["_51DEGREES_RESOURCE_KEY"] = o.ResourceKey,
                        // the endpoint includes the api/v4 path, as every
                        // reader of _51DEGREES_CLOUD_ENDPOINT expects
                        ["_51DEGREES_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["ASPNETCORE_URLS"] = $"http://localhost:{o.Port}",
                    }),
            };

        /// <summary>
        /// Options for starting <paramref name="app"/>, read from the
        /// environment.
        /// </summary>
        public static ExampleAppOptions BuildOptions(IExampleApp app, int port) =>
            BuildOptions(app, port, TestConfig.Instance());

        /// <summary>
        /// Options for starting <paramref name="app"/>.
        /// </summary>
        /// <remarks>
        /// An example that is already running (_51DEGREES_EXAMPLE_URL) was
        /// pointed at its cloud and given its key by whoever started it, and
        /// <see cref="ExternalExampleApp.StartAsync"/> ignores these options.
        /// So the endpoint and the key are passed on when they are set but
        /// are not required, which lets an on-premise example run the
        /// Contract tests with neither. An example this suite launches itself
        /// needs a key, and a missing one throws an
        /// <see cref="InvalidOperationException"/> naming the variable. It
        /// uses the public cloud service when no endpoint is set.
        /// </remarks>
        public static ExampleAppOptions BuildOptions(
            IExampleApp app, int port, TestConfig config)
        {
            string rootUrl;
            string resourceKey;
            if (app is ExternalExampleApp)
            {
                rootUrl = config.OptionalRootUrl;
                resourceKey = config.OptionalPaidResourceKey;
            }
            else
            {
                rootUrl = config.RootUrl;
                resourceKey = config.PaidResourceKey;
            }
            return new ExampleAppOptions(
                Port: port,
                CloudEndpoint: rootUrl == null ? null : new Uri(rootUrl),
                ResourceKey: resourceKey,
                ExtraEnv: new Dictionary<string, string>());
        }

        /// <summary>Per-language launch descriptors.</summary>
        public static readonly IReadOnlyDictionary<string, ExampleDescriptor> Descriptors =
            new Dictionary<string, ExampleDescriptor>
            {
                ["dotnet"] = new ExampleDescriptor(
                    Lang: "dotnet",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "51degrees-dotnet", "examples", "device-detection",
                        "Cloud", "GettingStarted-Web"),
                    Command: "dotnet",
                    Args: new[] { "run", "-c", "Release", "--no-launch-profile" },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 180,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        ["_51DEGREES_RESOURCE_KEY"] = o.ResourceKey,
                        // the endpoint must include the api/v4 path
                        ["_51DEGREES_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["ASPNETCORE_URLS"] = $"http://localhost:{o.Port}",
                        // run on a newer .NET runtime if the exact one isn't installed
                        ["DOTNET_ROLL_FORWARD"] = "Major",
                    }),
                ["java"] = new ExampleDescriptor(
                    Lang: "java",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "51degrees-java", "examples", "device-detection"),
                    Command: "java",
                    Args: new[] { "-jar" },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 120,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        [PortVar] = $"{o.Port}",
                        ["_51DEGREES_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["_51DEGREES_RESOURCE_KEY"] = o.ResourceKey,
                    },
                    BuildCommand: "mvn",
                    BuildArgs: new[] { "-pl", "web/getting-started.cloud", "-am", "package", "-DskipTests" },
                    RunArtifactGlob: "web/getting-started.cloud/target/*-jar-with-dependencies.jar",
                    BuildTimeoutSeconds: 600,
                    AlternativeJsonEndpointPaths: new[] { "/51Degrees.core.json" },
                    // the java page renders the client-side results into its
                    // Apple detection section rather than the shared container
                    ClientResultsElementId: "apple-detection"),
                ["node"] = new ExampleDescriptor(
                    Lang: "node",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "51degrees-node", "device-detection",
                        "fiftyone.devicedetection.cloud",
                        "examples", "cloud", "gettingstarted-web"),
                    Command: "node",
                    Args: new[] { "gettingStarted.js" },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 60,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        [PortVar] = $"{o.Port}",
                        ["_51DEGREES_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["_51DEGREES_RESOURCE_KEY"] = o.ResourceKey,
                    },
                    BuildCommand: "npm",
                    // package.json lives one level up from examples/cloud/gettingstarted-web.
                    // The pipeline packages are taken from the monorepo, so its
                    // pipeline/fiftyone.pipeline.core/javascript-templates submodule
                    // must be checked out before this runs. --install-links copies
                    // them in with their own dependencies; a symlink would resolve
                    // those from the monorepo, where nothing is installed.
                    BuildArgs: new[] { "install", "--install-links", "--prefix", "../../.." },
                    BuildTimeoutSeconds: 300,
                    JsonEndpointPath: "/json"),
                ["python"] = new ExampleDescriptor(
                    Lang: "python",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "51degrees-python", "device-detection",
                        "fiftyone_devicedetection_examples"),
                    // the venv's python (absolute path)
                    Command: VenvPython(Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "51degrees-python", "device-detection",
                        "fiftyone_devicedetection_examples",
                        ".venv")),
                    Args: new[] { "-m", "fiftyone_devicedetection_examples.cloud.gettingstarted_web" },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 60,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        [PortVar] = $"{o.Port}",
                        ["_51DEGREES_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["_51DEGREES_RESOURCE_KEY"] = o.ResourceKey,
                    },
                    BuildCommand: "bash",
                    // a Windows python install may have no working "python3",
                    // so fall back to "python" to create the venv, then use
                    // the venv's own interpreter, whose path differs by OS
                    BuildArgs: new[]
                    {
                        "-c",
                        "(python3 -m venv .venv || python -m venv .venv) && " +
                        VenvPython(".venv").Replace(Path.DirectorySeparatorChar, '/') +
                        " -m pip install -e .",
                    },
                    BuildTimeoutSeconds: 600,
                    JsonEndpointPath: "/json"),
                ["php"] = new ExampleDescriptor(
                    Lang: "php",
                    WorkingDir: Path.Combine(
                        RepoPaths.SiblingsRoot,
                        "51degrees-php", "device-detection", "cloud", "examples"),
                    Command: "bash",
                    // clear the cached pipeline so the endpoint override takes effect
                    Args: new[]
                    {
                        "-c",
                        "rm -f cloud/classesgettingStartedWeb.pipeline cloud/classes/gettingStartedWeb.pipeline; " +
                        "exec php -S localhost:$" + PortVar + " cloud/gettingStartedWeb.php",
                    },
                    ReadinessPath: "/",
                    StartupTimeoutSeconds: 60,
                    BuildEnv: o => new Dictionary<string, string>
                    {
                        [PortVar] = $"{o.Port}",
                        ["_51DEGREES_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["_51DEGREES_RESOURCE_KEY"] = o.ResourceKey,
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
                        [PortVar] = $"{o.Port}",
                        ["_51DEGREES_CLOUD_ENDPOINT"] = new Uri(o.CloudEndpoint, "api/v4/").ToString(),
                        ["_51DEGREES_RESOURCE_KEY"] = o.ResourceKey,
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

        /// <summary>
        /// Path of the python interpreter inside the virtual environment at
        /// <paramref name="venv"/>. A Windows virtual environment keeps it in
        /// Scripts/python.exe, everywhere else it is bin/python.
        /// </summary>
        public static string VenvPython(string venv) =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(venv, "Scripts", "python.exe")
                : Path.Combine(venv, "bin", "python");
    }
}
