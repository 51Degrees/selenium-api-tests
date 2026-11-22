using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Selenium;

/// <summary>
/// The settings <see cref="BrowserDrivers"/> gives every session: headless
/// and insecure certificates accepted, the Chromium arguments Chrome and Edge
/// need on the runners, quiet driver services, and the session time limit
/// for remote sessions and local Edge ones as well as local Chrome and
/// Firefox ones. The members are reached by name, so a build without them
/// still runs these and they fail, rather than the build stopping.
/// </summary>
[TestClass]
public class DriverSettingsTests
{
    /// <summary>
    /// Field of Selenium's <see cref="HttpCommandExecutor"/> that holds the
    /// time limit of each command, read because Selenium offers no public
    /// way to read it.
    /// </summary>
    private const string LimitFieldName = "serverResponseTimeout";

    private const string SeleniumUrlVariable = BrowserDrivers.SeleniumUrlVariable;

    private static readonly string[] ChromeArguments =
        { "--no-sandbox", "--disable-dev-shm-usage" };

    /// <summary>
    /// Each Chromium argument is added once, one the caller already set is
    /// not added again, and the caller's own arguments are kept.
    /// </summary>
    [TestMethod]
    public void ChromeArguments_AddedOnce_CallersOwnKept()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless");
        options.AddArgument("--no-sandbox");
        Invoke("AddChromeArguments", options);
        foreach (var argument in ChromeArguments)
        {
            Assert.AreEqual(
                1,
                options.Arguments.Count(a => a == argument),
                $"{argument} must be in the options exactly once. They "
                + $"hold: {string.Join(" ", options.Arguments)}");
        }
        Assert.IsTrue(
            options.Arguments.Contains("--headless"),
            "the caller's own argument must be kept.");
    }

    /// <summary>
    /// A remote session is given the same time limit for each command as a
    /// local Chrome or Firefox session, rather than Selenium's 60 seconds.
    /// A stand-in for a remote Selenium answers the session's two requests,
    /// so no browser is needed.
    /// </summary>
    [TestMethod]
    public void RemoteSession_HasTheSessionLimit()
    {
        using var remote = new StandInRemote();
        var driver = (WebDriver)Invoke(
            "CreateRemote", remote.Url, new ChromeOptions());
        try
        {
            Assert.AreEqual(
                TimeSpan.FromSeconds(BrowserDrivers.SessionTimeoutSeconds),
                Limit(driver),
                "a remote session must have the session limit.");
        }
        finally
        {
            driver.Quit();
        }
    }

    /// <summary>
    /// The 51Did harness starts its browsers itself rather than through
    /// <see cref="BrowserDrivers.CreateChrome"/>, and its remote Chrome and
    /// Firefox sessions get the same limit. _51DEGREES_SELENIUM_URL names the stand-in
    /// for this test only, and is put back as it was.
    /// </summary>
    [TestMethod]
    public void HarnessRemoteSessions_HaveTheSessionLimit()
    {
        using var remote = new StandInRemote();
        var before = Environment.GetEnvironmentVariable(SeleniumUrlVariable);
        Environment.SetEnvironmentVariable(
            SeleniumUrlVariable, remote.Url.ToString());
        try
        {
            foreach (var start in new Func<IWebDriver>[]
            {
                () => Browser51Did.Harness.NewChrome(),
                () => Browser51Did.Harness.NewFirefox(),
            })
            {
                var driver = (WebDriver)start();
                try
                {
                    Assert.AreEqual(
                        TimeSpan.FromSeconds(
                            BrowserDrivers.SessionTimeoutSeconds),
                        Limit(driver),
                        "a remote session the harness starts must have the "
                        + "session limit.");
                }
                finally
                {
                    driver.Quit();
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(SeleniumUrlVariable, before);
        }
    }

    /// <summary>
    /// The Chrome driver service writes no verbose or appended log, prints
    /// no start up banner and opens no console window.
    /// </summary>
    [TestMethod]
    public void ChromeService_IsQuiet()
    {
        using var service = (ChromeDriverService)Invoke(
            "ChromeService", (object)null);
        Assert.IsFalse(service.EnableVerboseLogging, "verbose logging");
        Assert.IsFalse(service.EnableAppendLog, "appended logging");
        Assert.IsTrue(
            service.SuppressInitialDiagnosticInformation, "start up banner");
        Assert.IsTrue(service.HideCommandPromptWindow, "console window");
    }

    /// <summary>
    /// The Firefox driver service prints no start up banner and opens no
    /// console window.
    /// </summary>
    [TestMethod]
    public void FirefoxService_IsQuiet()
    {
        using var service = (FirefoxDriverService)Invoke(
            "FirefoxService", (object)null);
        Assert.IsTrue(
            service.SuppressInitialDiagnosticInformation, "start up banner");
        Assert.IsTrue(service.HideCommandPromptWindow, "console window");
    }

    /// <summary>
    /// A Chrome started through <see cref="BrowserDrivers.CreateChrome"/>
    /// with options that name only headless carries both Chromium
    /// arguments.
    /// </summary>
    [TestMethod, TestCategory("Browser")]
    public void CreateChrome_GivesTheChromeArguments()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless");
        var driver = BrowserDrivers.CreateChrome(options);
        try
        {
            foreach (var argument in ChromeArguments)
            {
                Assert.IsTrue(
                    options.Arguments.Contains(argument),
                    $"CreateChrome must give {argument}. The options hold: "
                    + string.Join(" ", options.Arguments));
            }
        }
        finally
        {
            driver.Quit();
        }
    }

    /// <summary>
    /// The 51Did harness's local Chrome and Firefox start, load a page, and
    /// have the session limit. Its tests need a cloud and skip without one,
    /// so this is what starts the harness's own browsers on every runner.
    /// </summary>
    [TestMethod, TestCategory("Browser")]
    public void HarnessLocalSessions_StartWithTheSessionLimit()
    {
        foreach (var start in new Func<IWebDriver>[]
        {
            () => Browser51Did.Harness.NewChrome(),
            () => Browser51Did.Harness.NewFirefox(),
        })
        {
            var driver = (WebDriver)start();
            try
            {
                driver.Navigate().GoToUrl("about:blank");
                Assert.AreEqual(
                    TimeSpan.FromSeconds(BrowserDrivers.SessionTimeoutSeconds),
                    Limit(driver),
                    $"a local {driver.Capabilities.GetCapability("browserName")} "
                    + "session the harness starts must have the session "
                    + "limit.");
            }
            finally
            {
                driver.Quit();
            }
        }
    }

    /// <summary>
    /// Chrome and Edge options are prepared with headless, insecure
    /// certificates accepted and both Chromium arguments, each once.
    /// </summary>
    [TestMethod]
    public void Prepare_ChromiumOptions_GetEverySettingOnce()
    {
        foreach (var options in new ChromiumOptions[]
        {
            new ChromeOptions(), new EdgeOptions(),
        })
        {
            Invoke("Prepare", options);
            Invoke("Prepare", options);
            var name = options.GetType().Name;
            Assert.IsTrue(
                options.AcceptInsecureCertificates,
                $"{name} must accept insecure certificates.");
            foreach (var argument in ChromeArguments.Append("--headless"))
            {
                Assert.AreEqual(
                    1,
                    options.Arguments.Count(a => a == argument),
                    $"{name} must carry {argument} exactly once. It holds: "
                    + string.Join(" ", options.Arguments));
            }
        }
    }

    /// <summary>
    /// A headless argument the caller already chose, such as
    /// --headless=new, is not joined by a second one.
    /// </summary>
    [TestMethod]
    public void Prepare_CallersHeadless_IsKept()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        Invoke("Prepare", options);
        Assert.AreEqual(
            1,
            options.Arguments.Count(a => a.StartsWith(
                "--headless", StringComparison.Ordinal)),
            "only the caller's headless argument may be there. The options "
            + $"hold: {string.Join(" ", options.Arguments)}");
    }

    /// <summary>
    /// Firefox options are prepared to accept insecure certificates.
    /// Selenium offers no way to read back a Firefox argument, so headless
    /// is checked by the Firefox sessions the browser tests start.
    /// </summary>
    [TestMethod]
    public void Prepare_FirefoxOptions_AcceptInsecureCertificates()
    {
        var options = new FirefoxOptions();
        Invoke("Prepare", options);
        Assert.IsTrue(options.AcceptInsecureCertificates);
    }

    /// <summary>
    /// The Edge driver service is as quiet as Chrome's.
    /// </summary>
    [TestMethod]
    public void EdgeService_IsQuiet()
    {
        using var service = (EdgeDriverService)Invoke(
            "EdgeService", (object)null);
        Assert.IsFalse(service.EnableVerboseLogging, "verbose logging");
        Assert.IsFalse(service.EnableAppendLog, "appended logging");
        Assert.IsTrue(
            service.SuppressInitialDiagnosticInformation, "start up banner");
        Assert.IsTrue(service.HideCommandPromptWindow, "console window");
    }

    /// <summary>
    /// When _51DEGREES_SELENIUM_URL names a Selenium that cannot be reached, the
    /// Chrome, Firefox and Edge browser test classes fail rather than report
    /// inconclusive, because a missing browser on a developer's machine is
    /// not a fault and a grid that will not hand one out is.
    /// </summary>
    [TestMethod]
    public void BrowserTestClasses_RemoteThatCannotBeReached_Fail()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var before = Environment.GetEnvironmentVariable(SeleniumUrlVariable);
        Environment.SetEnvironmentVariable(
            SeleniumUrlVariable, $"http://localhost:{port}/");
        try
        {
            foreach (var (name, init, cleanup) in new (string, Action, Action)[]
            {
                ("ChromeTests",
                    () => Browsers.ChromeTests.ClassInit(null),
                    Browsers.ChromeTests.ClassCleanup),
                ("GeckoTests",
                    () => Browsers.GeckoTests.ClassInit(null),
                    Browsers.GeckoTests.ClassCleanup),
                ("EdgeTests",
                    () => Browsers.EdgeTests.ClassInit(null),
                    Browsers.EdgeTests.ClassCleanup),
            })
            {
                try
                {
                    Assert.Throws<WebDriverException>(
                        init,
                        $"{name} must fail when the grid cannot be reached, "
                        + "not report inconclusive.");
                }
                finally
                {
                    cleanup();
                }
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(SeleniumUrlVariable, before);
        }
    }

    /// <summary>
    /// A local Edge has both Chromium arguments and the session limit.
    /// Microsoft ships no Edge for ARM64 Linux, so there it is
    /// inconclusive with the reason <see cref="BrowserDrivers.CreateEdge"/>
    /// gives.
    /// </summary>
    [TestMethod, TestCategory("Browser")]
    public void CreateEdge_HasTheArgumentsAndTheSessionLimit()
    {
        var options = new EdgeOptions();
        WebDriver driver;
        try
        {
            driver = BrowserDrivers.CreateEdge(options);
        }
        catch (WebDriverException e) when (BrowserDrivers.IsLinuxArm64)
        {
            Assert.Inconclusive(e.Message);
            return;
        }
        try
        {
            foreach (var argument in ChromeArguments)
            {
                Assert.IsTrue(
                    options.Arguments.Contains(argument),
                    $"CreateEdge must give {argument}. The options hold: "
                    + string.Join(" ", options.Arguments));
            }
            Assert.AreEqual(
                TimeSpan.FromSeconds(BrowserDrivers.SessionTimeoutSeconds),
                Limit(driver),
                "a local Edge session must have the session limit.");
        }
        finally
        {
            driver.Quit();
        }
    }

    /// <summary>
    /// Calls a static member of <see cref="BrowserDrivers"/> by name.
    /// </summary>
    private static object Invoke(string name, params object[] arguments)
    {
        var method = typeof(BrowserDrivers).GetMethod(
            name,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Assert.Fail($"{nameof(BrowserDrivers)} has no {name}.");
        }
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            throw e.InnerException;
        }
    }

    /// <summary>
    /// The time limit Selenium gives each command of
    /// <paramref name="driver"/>.
    /// </summary>
    private static TimeSpan Limit(WebDriver driver)
    {
        var executor = driver.CommandExecutor;
        if (executor is DriverServiceCommandExecutor service)
        {
            executor = service.HttpExecutor;
        }
        var field = typeof(HttpCommandExecutor).GetField(
            LimitFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null || !(executor is HttpCommandExecutor))
        {
            Assert.Fail(
                $"The Selenium in use keeps no {LimitFieldName} in its "
                + $"{nameof(HttpCommandExecutor)}, so {nameof(Limit)} needs "
                + "changing to read the limit where it is now.");
        }
        return (TimeSpan)field.GetValue(executor);
    }

    /// <summary>
    /// Answers the two requests a remote session makes, creating it and
    /// deleting it, and nothing else.
    /// </summary>
    private sealed class StandInRemote : IDisposable
    {
        private const string SessionId = "stand-in";

        private readonly HttpListener _listener = new HttpListener();
        private readonly CancellationTokenSource _stop =
            new CancellationTokenSource();
        private readonly Task _serving;

        public StandInRemote()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = new Uri($"http://localhost:{port}/");
            _listener.Prefixes.Add(Url.ToString());
            _listener.Start();
            _serving = Task.Run(Serve);
        }

        public Uri Url { get; }

        private async Task Serve()
        {
            while (_stop.IsCancellationRequested == false)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception) when (_stop.IsCancellationRequested)
                {
                    return;
                }
                var body = context.Request.HttpMethod == "POST"
                    && context.Request.Url.AbsolutePath.TrimEnd('/')
                        == "/session"
                    ? "{\"value\":{\"sessionId\":\"" + SessionId + "\","
                        + "\"capabilities\":{\"browserName\":\"chrome\"}}}"
                    : "{\"value\":null}";
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentType =
                    "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            try
            {
                _serving.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // The listener stopping ends the wait for a request.
            }
            _listener.Close();
        }
    }
}
