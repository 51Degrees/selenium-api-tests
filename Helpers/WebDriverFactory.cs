using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;

/// <summary>
/// Single place where every WebDriver used by these tests is created.
///
/// Browser tests used to time out intermittently on GitHub runners because the
/// driver was built with Selenium's default 60 second command timeout and
/// without the arguments a headless Chromium needs on a constrained CI machine.
/// Those settings existed only on the <c>SELENIUM_URL</c> path, which CI does
/// not take, so in practice nothing applied them. Creating every driver here
/// means the local and remote paths cannot drift apart again.
/// </summary>
public static class WebDriverFactory
{
    /// <summary>
    /// How long to wait for a single WebDriver command before giving up.
    /// Selenium's default is 60 seconds, which a healthy but slow browser on a
    /// shared CI runner can exceed.
    /// </summary>
    public static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(120);

    private const string HeadlessArgument = "--headless";

    /// <summary>
    /// Arguments a headless Chromium needs to be stable in a container or on a
    /// CI runner: no sandbox (no user namespaces available), no GPU (no
    /// display), and /dev/shm not used for shared memory (it is typically too
    /// small, which shows up as the browser dying mid-session).
    /// </summary>
    private static readonly string[] ChromiumStabilityArguments =
    {
        "--no-sandbox",
        "--disable-gpu",
        "--disable-dev-shm-usage",
    };

    /// <summary>
    /// Creates a driver for the supplied options, hardened for CI.
    /// </summary>
    /// <remarks>
    /// Callers configure whatever is specific to their test (mobile emulation,
    /// a user agent override, logging preferences) and leave the headless,
    /// stability and timeout concerns to this method. Returns a
    /// <see cref="RemoteWebDriver"/> when <c>SELENIUM_URL</c> is set and a
    /// locally launched driver otherwise.
    /// </remarks>
    /// <param name="options">Browser options to launch with.</param>
    /// <returns>A ready-to-use driver.</returns>
    public static WebDriver Create(DriverOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        PrepareOptions(options);

        if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
        {
            ExternalSeleniumHelper.AddExternalSeleniumArguments(options);
            return new RemoteWebDriver(
                new Uri(seleniumUrl), options.ToCapabilities(), CommandTimeout);
        }

        return CreateLocal(options);
    }

    /// <summary>
    /// Applies the settings every driver in this suite wants, whether it runs
    /// locally or on a grid. Safe to call on options a test has already
    /// configured: a Chromium argument that is present is not added again.
    /// </summary>
    /// <param name="options">Browser options to prepare.</param>
    internal static void PrepareOptions(DriverOptions options)
    {
        options.AcceptInsecureCertificates = true;

        switch (options)
        {
            case ChromiumOptions chromiumOptions:
                AddArgumentIfMissing(chromiumOptions, HeadlessArgument);
                foreach (var argument in ChromiumStabilityArguments)
                {
                    AddArgumentIfMissing(chromiumOptions, argument);
                }
                break;

            // The Chromium stability arguments are not Firefox arguments, so
            // Firefox gets headless and nothing else. Its sandbox and shared
            // memory behaviour has never been the problem here.
            //
            // Selenium exposes no way to read arguments back off FirefoxOptions,
            // so this cannot be guarded the way the Chromium path is. Call sites
            // therefore leave headless to this method, and a repeated -headless
            // would in any case be harmless to Firefox.
            case FirefoxOptions firefoxOptions:
                firefoxOptions.AddArgument(HeadlessArgument);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(options),
                    $"DriverOptions of type {options.GetType()} are not supported");
        }
    }

    private static void AddArgumentIfMissing(ChromiumOptions options, string argument)
    {
        if (options.Arguments.Contains(argument) == false)
        {
            options.AddArgument(argument);
        }
    }

    /// <summary>
    /// Launches a driver process on this machine. The service is configured to
    /// keep quiet - verbose and appended driver logs cost time and disk on CI
    /// and have never been read. The driver takes ownership of the service, so
    /// it is only disposed here if construction fails and no driver exists to
    /// do it.
    /// </summary>
    /// <param name="options">Prepared browser options.</param>
    /// <returns>A locally launched driver.</returns>
    private static WebDriver CreateLocal(DriverOptions options)
    {
        switch (options)
        {
            case ChromeOptions chromeOptions:
            {
                var service = ChromeDriverService.CreateDefaultService();
                Quieten(service);
                return Build(service,
                    () => new ChromeDriver(service, chromeOptions, CommandTimeout));
            }

            case EdgeOptions edgeOptions:
            {
                var service = EdgeDriverService.CreateDefaultService();
                Quieten(service);
                return Build(service,
                    () => new EdgeDriver(service, edgeOptions, CommandTimeout));
            }

            case FirefoxOptions firefoxOptions:
            {
                var service = FirefoxDriverService.CreateDefaultService();
                service.HideCommandPromptWindow = true;
                service.SuppressInitialDiagnosticInformation = true;
                return Build(service,
                    () => new FirefoxDriver(service, firefoxOptions, CommandTimeout));
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(options),
                    $"DriverOptions of type {options.GetType()} are not supported");
        }
    }

    private static void Quieten(ChromiumDriverService service)
    {
        service.EnableVerboseLogging = false;
        service.EnableAppendLog = false;
        service.HideCommandPromptWindow = true;
        service.SuppressInitialDiagnosticInformation = true;
    }

    private static WebDriver Build(DriverService service, Func<WebDriver> create)
    {
        try
        {
            return create();
        }
        catch
        {
            // Nothing took ownership of the service, so it would otherwise leak
            // a driver process for the rest of the test run.
            service.Dispose();
            throw;
        }
    }
}
