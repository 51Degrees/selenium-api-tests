#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Examples;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// The routes every language's demo serves, as the dotnet demo in
/// device-detection-dotnet-examples defines them. A test names the page it
/// loads by one of these and nothing else, so the same test drives any
/// demo that serves the same pages.
/// </summary>
public static class Routes
{
    /// <summary>PMP, then the client script.</summary>
    public const string Common = "common";

    /// <summary>The client script, then PMP.</summary>
    public const string CommonScriptFirst = "common-script-first";

    /// <summary>PMP, the client script and the change watcher.</summary>
    public const string Change = "change";

    /// <summary>The first of two pages carrying what <see cref="Change"/> does.</summary>
    public const string TwoOne = "two/one";

    /// <summary>The second of those two pages.</summary>
    public const string TwoTwo = "two/two";

    /// <summary>The stub consent platform, then the client script.</summary>
    public const string Consent = "consent";

    /// <summary>The client script alone.</summary>
    public const string NoPlatform = "no-platform";

    /// <summary>PMP alone, with no client script tag.</summary>
    public const string PlatformOnly = "platform-only";

    /// <summary>
    /// PMP alone, naming the client script's object
    /// <see cref="Harness.NamedObject"/>.
    /// </summary>
    public const string NamedObject = "named-object";
}

/// <summary>
/// How a demo's pages get their client script, being the route prefix the
/// demo serves them under.
/// </summary>
public sealed class DemoMode
{
    /// <summary>
    /// Pages that load the client script straight from the cloud, which
    /// posts its evidence to the cloud's own endpoint.
    /// </summary>
    public static readonly DemoMode Cloud = new("cloud", null);

    /// <summary>
    /// Pages whose client script the demo's own pipeline serves, which
    /// posts to that pipeline's endpoint, being the path the demo's
    /// descriptor names.
    /// </summary>
    public static readonly DemoMode Pipeline = new("pipeline", "descriptor");

    /// <summary>The route prefix, and the name DEMO_MODE takes.</summary>
    public string Name { get; }

    private readonly string? _pathFromDescriptor;

    private DemoMode(string name, string? pathFromDescriptor)
    {
        Name = name;
        _pathFromDescriptor = pathFromDescriptor;
    }

    /// <summary>
    /// The path the client script posts its evidence to on this mode's
    /// pages, which is how a test tells the client script's own requests
    /// from everything else a page does.
    /// </summary>
    public string JsonEndpointPath(ExampleDescriptor? descriptor)
        => _pathFromDescriptor is null
            ? "/api/v4/json"
            : descriptor?.JsonEndpointPath
                ?? throw new InvalidOperationException(
                    "The pipeline mode needs a registered demo, because its "
                    + "descriptor names the path the client script posts to.");
}

/// <summary>
/// The demo under test, started once for the whole run.
/// <para>
/// It is the demo at DEMO_URL where that is set, and otherwise the DEMO_LANG
/// demo, dotnet unless another is named, which the suite launches from its
/// sibling checkout through <see cref="ExampleApps.TryCreateDemo"/>, the way
/// the Contract tests launch an example. It is started with exactly the two
/// input variables every language's demo reads, 51DEGREES_RESOURCE_KEY and
/// 51DEGREES_CLOUD_ENDPOINT, taken from this run's own environment (the key
/// from _51DEGREES_RESOURCE_KEY_51DID where 51DEGREES_RESOURCE_KEY is
/// unset), and with the runtime's own way of choosing a port. DEMO_MODE chooses the
/// route prefix, cloud unless pipeline is named.
/// </para>
/// </summary>
public sealed class Demo
{
    private static readonly Lazy<Demo> ChosenOnce =
        new(Choose, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// The demo this run tests. Deciding starts nothing, and
    /// <see cref="EnsureStarted"/> starts it.
    /// </summary>
    public static Demo Chosen => ChosenOnce.Value;

    private readonly IExampleApp? _app;
    private readonly ExampleDescriptor? _descriptor;
    private readonly string? _unavailable;
    private readonly object _startLock = new();
    private bool _started;
    private string? _startFailure;

    /// <summary>Which pages are loaded.</summary>
    public DemoMode Mode { get; }

    /// <summary>The demo's name, for a message.</summary>
    public string Name { get; }

    private Demo(
        IExampleApp? app,
        ExampleDescriptor? descriptor,
        DemoMode mode,
        string name,
        string? unavailable)
    {
        _app = app;
        _descriptor = descriptor;
        Mode = mode;
        Name = name;
        _unavailable = unavailable;
    }

    private static Demo Choose()
    {
        var modeName = Environment.GetEnvironmentVariable("DEMO_MODE");
        var mode = string.Equals(
            modeName, DemoMode.Pipeline.Name, StringComparison.OrdinalIgnoreCase)
            ? DemoMode.Pipeline
            : DemoMode.Cloud;
        var name = $"the {ExampleApps.SelectedDemoLang} demo's /{mode.Name}/ pages";
        return ExampleApps.TryCreateDemo(
                out var app, out var descriptor, out var skipReason)
            ? new Demo(app, descriptor, mode, name, null)
            : new Demo(null, descriptor, mode, name, skipReason);
    }

    /// <summary>
    /// The path the client script posts its evidence to on the pages being
    /// loaded.
    /// </summary>
    public string JsonEndpointPath => Mode.JsonEndpointPath(_descriptor);

    /// <summary>
    /// Starts the demo where it has not been started, once for the whole
    /// run, because launching one costs far more than any test here. A demo
    /// that fails to start fails every test with the same reason.
    /// </summary>
    public void EnsureStarted()
    {
        lock (_startLock)
        {
            if (_startFailure != null)
            {
                Assert.Fail(_startFailure);
            }
            if (_started)
            {
                return;
            }
            if (_app is null)
            {
                _startFailure = $"No demo can be started. {_unavailable}";
                Assert.Fail(_startFailure);
                return;
            }
            try
            {
                _app.StartAsync(
                        new ExampleAppOptions(
                            TestHelpers.GetRandomUnusedPort(),
                            new Uri(Harness.CloudUrl + "/"),
                            Harness.Resource!,
                            new Dictionary<string, string>()),
                        CancellationToken.None)
                    .GetAwaiter().GetResult();
                _started = true;
            }
            catch (Exception error)
            {
                // The demo's own output is in the message, and a page it
                // logged could carry the resource key.
                _startFailure = Harness.Redacted(
                    $"{Name} could not be started, so no test ran against "
                    + $"it. {error.Message}");
                Assert.Fail(_startFailure);
            }
        }
    }

    /// <summary>
    /// Stops the demo where this run started it, and does nothing where no
    /// test ever asked for one, so a run of another category is left alone.
    /// </summary>
    public static void StopIfStarted()
    {
        if (ChosenOnce.IsValueCreated == false)
        {
            return;
        }
        var chosen = ChosenOnce.Value;
        lock (chosen._startLock)
        {
            if (chosen._app is null || chosen._started == false)
            {
                return;
            }
            chosen._app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            chosen._started = false;
        }
    }

    /// <summary>
    /// The address of one of the demo's pages as one of the publisher
    /// sites. The cache buster is on every navigation, because a page held
    /// in the browser's cache would carry the recorder from an earlier test.
    /// </summary>
    public string PageUrl(string site, string route)
    {
        EnsureStarted();
        return $"http://{site}:{_app!.BaseUrl.Port}/{Mode.Name}/{route}"
            + $"?v={DateTime.UtcNow.Ticks}";
    }

    /// <summary>
    /// Where the guard fetches the client script from, server to server,
    /// being what the pages load. On the cloud pages that is the cloud's
    /// script for the resource key, and on the pipeline pages it is the
    /// demo's own.
    /// </summary>
    public string ClientScriptFetchUrl()
    {
        if (Mode == DemoMode.Cloud)
        {
            return $"{Harness.CloudUrl}/api/v4/{Harness.Resource}.js";
        }
        EnsureStarted();
        return new Uri(_app!.BaseUrl, "51Degrees.core.js").ToString();
    }
}

/// <summary>
/// Stops the demo this run started once every test has finished.
/// </summary>
[TestClass]
public class Browser51DidRunCleanup
{
    /// <summary>
    /// MSTest allows one assembly clean up in a test assembly and this is
    /// it, so anything else that has to happen at the end of a run belongs
    /// in this method rather than beside it. A demo left running would hold
    /// its port after the run.
    /// </summary>
    [AssemblyCleanup]
    public static void StopTheDemo() => Demo.StopIfStarted();
}
