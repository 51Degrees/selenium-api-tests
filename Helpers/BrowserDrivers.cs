using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Manager;
using OpenQA.Selenium.Remote;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers
{
    /// <summary>
    /// Creates the web drivers the tests use, from one place, so every test
    /// finds a browser the same way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A driver the machine already provides is used first, because a machine
    /// that ships one is faster and steadier than downloading one for every
    /// run. GitHub's Linux runner images name theirs in CHROMEWEBDRIVER,
    /// GECKOWEBDRIVER and EDGEWEBDRIVER, and the ARM64 images set only
    /// GECKOWEBDRIVER, as they carry Firefox and geckodriver but no Chrome,
    /// no Chromium and no Edge.
    /// </para>
    /// <para>
    /// When the machine provides no driver, Selenium Manager is left to fetch
    /// one. That is a helper program shipped inside the Selenium.WebDriver
    /// package, and up to version 4.48 the only Linux build of it was for x64,
    /// under a folder named for Linux with no architecture in the name. On an
    /// ARM64 Linux machine that build was picked and could not start, so every
    /// browser test died with "Exec format error" before a browser was ever
    /// launched. Version 4.49 splits the folder by architecture and adds an
    /// ARM64 build, which is why this project needs 4.49 or later.
    /// </para>
    /// <para>
    /// Giving Selenium a driver service that already knows its path stops it
    /// calling Selenium Manager at all, which is the behaviour the first case
    /// relies on.
    /// </para>
    /// <para>
    /// CloakBrowser is the exception, as it is never started here. It runs in
    /// its vendor's container, and a driver attaches to it at the address
    /// CLOAK_DEBUGGER_ADDRESS names. That driver has to match CloakBrowser's
    /// version of Chromium, which is not the version of the Chrome on this
    /// machine, so a driver found on the path is never used for it.
    /// </para>
    /// </remarks>
    public static class BrowserDrivers
    {
        /// <summary>
        /// Set to the address of a Selenium grid or container to drive a
        /// browser there instead of on this machine.
        /// </summary>
        public const string SeleniumUrlVariable = "SELENIUM_URL";

        /// <summary>
        /// Directory holding chromedriver, or the driver itself. GitHub's
        /// Linux x64 runner images set this variable.
        /// </summary>
        public const string ChromeDriverVariable = "CHROMEWEBDRIVER";

        /// <summary>
        /// Directory holding geckodriver, or the driver itself. GitHub's
        /// Linux runner images set this variable on both architectures.
        /// </summary>
        public const string GeckoDriverVariable = "GECKOWEBDRIVER";

        /// <summary>
        /// Directory holding msedgedriver, or the driver itself. GitHub's
        /// Linux x64 runner images set this variable.
        /// </summary>
        public const string EdgeDriverVariable = "EDGEWEBDRIVER";

        /// <summary>Path to the Chrome or Chromium binary to drive.</summary>
        public const string ChromeBinaryVariable = "CHROME_BIN";

        /// <summary>Path to the Firefox binary to drive.</summary>
        public const string FirefoxBinaryVariable = "FIREFOX_BIN";

        /// <summary>Path to the Edge binary to drive.</summary>
        public const string EdgeBinaryVariable = "EDGE_BIN";

        /// <summary>
        /// Address of a running CloakBrowser to attach to, as host:port, for
        /// example 127.0.0.1:9222. The Cloak tests are skipped without it.
        /// </summary>
        public const string CloakDebuggerAddressVariable =
            "CLOAK_DEBUGGER_ADDRESS";

        /// <summary>
        /// Directory holding a chromedriver of CloakBrowser's major version,
        /// or the driver itself. Selenium Manager fetches one when it is
        /// unset.
        /// </summary>
        public const string CloakDriverVariable = "CLOAKWEBDRIVER";

        /// <summary>
        /// The reason a Cloak test is skipped, naming the variable to set.
        /// </summary>
        public const string CloakNotConfiguredMessage =
            "No CloakBrowser to attach to. Set "
            + CloakDebuggerAddressVariable + " to the host and port of one "
            + "that is running, for example 127.0.0.1:9222. The README says "
            + "how to start one.";

        // Long enough for the first request to start the browser.
        private const int CloakVersionTimeoutSeconds = 60;

        /// <summary>File names a Chrome driver goes by.</summary>
        public static readonly string[] ChromeDriverNames =
            { "chromedriver", "chromedriver.exe" };

        /// <summary>File names a Firefox driver goes by.</summary>
        public static readonly string[] GeckoDriverNames =
            { "geckodriver", "geckodriver.exe" };

        /// <summary>File names an Edge driver goes by.</summary>
        public static readonly string[] EdgeDriverNames =
            { "msedgedriver", "msedgedriver.exe" };

        // Chromium is what an ARM64 Linux machine is most likely to have, so
        // it is looked for alongside Chrome.
        private static readonly string[] _chromeBinaryNames =
        {
            "google-chrome", "google-chrome-stable", "chromium-browser",
            "chromium", "chrome.exe",
        };

        private static readonly string[] _firefoxBinaryNames =
            { "firefox", "firefox.exe" };

        private static readonly string[] _edgeBinaryNames =
            { "microsoft-edge", "microsoft-edge-stable", "msedge.exe" };

        /// <summary>
        /// True on ARM64 Linux, where Microsoft publishes no Edge browser and
        /// no Edge driver, so an Edge test there can only be skipped.
        /// </summary>
        public static bool IsLinuxArm64 { get; } =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        /// <summary>
        /// Creates a Chrome or Chromium driver.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        /// <returns>A driver, which the caller quits.</returns>
        public static WebDriver CreateChrome(ChromeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (TryCreateRemote(options, out var remote))
            {
                return remote;
            }
            SetBinaryLocation(options, ChromeBinaryVariable, _chromeBinaryNames);
            var driverPath = FindDriver(ChromeDriverVariable, ChromeDriverNames);
            if (driverPath != null)
            {
                return new ChromeDriver(
                    ChromeDriverService.CreateDefaultService(driverPath),
                    options);
            }
            return Fetched(
                () => new ChromeDriver(options),
                "Chrome", ChromeDriverVariable, ChromeDriverNames);
        }

        /// <summary>
        /// Creates a Firefox driver.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        /// <returns>A driver, which the caller quits.</returns>
        public static WebDriver CreateFirefox(FirefoxOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (TryCreateRemote(options, out var remote))
            {
                return remote;
            }
            SetBinaryLocation(
                options, FirefoxBinaryVariable, _firefoxBinaryNames);
            var driverPath = FindDriver(GeckoDriverVariable, GeckoDriverNames);
            if (driverPath != null)
            {
                return new FirefoxDriver(
                    FirefoxDriverService.CreateDefaultService(driverPath),
                    options);
            }
            return Fetched(
                () => new FirefoxDriver(options),
                "Firefox", GeckoDriverVariable, GeckoDriverNames);
        }

        /// <summary>
        /// Creates an Edge driver.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        /// <returns>A driver, which the caller quits.</returns>
        public static WebDriver CreateEdge(EdgeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (TryCreateRemote(options, out var remote))
            {
                return remote;
            }
            var driverPath = FindDriver(EdgeDriverVariable, EdgeDriverNames);
            if (driverPath == null && IsLinuxArm64)
            {
                throw new WebDriverException(
                    "Edge cannot run on ARM64 Linux, because Microsoft "
                    + "publishes no Edge browser and no Edge driver for it. "
                    + $"Set {SeleniumUrlVariable} to a Selenium that has "
                    + "Edge, or run the Edge tests on another architecture.");
            }
            SetBinaryLocation(options, EdgeBinaryVariable, _edgeBinaryNames);
            if (driverPath != null)
            {
                return new EdgeDriver(
                    EdgeDriverService.CreateDefaultService(driverPath),
                    options);
            }
            return Fetched(
                () => new EdgeDriver(options),
                "Edge", EdgeDriverVariable, EdgeDriverNames);
        }

        /// <summary>
        /// True when CLOAK_DEBUGGER_ADDRESS names a CloakBrowser to attach to.
        /// </summary>
        public static bool IsCloakConfigured =>
            CloakDebuggerAddress(Environment.GetEnvironmentVariable) != null;

        /// <summary>
        /// Attaches a driver to the running CloakBrowser that
        /// CLOAK_DEBUGGER_ADDRESS names.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The browser was started by its container and outlives the driver,
        /// so arguments in <paramref name="options"/> never reach it, and
        /// SELENIUM_URL does not apply. Mobile emulation cannot be asked for
        /// either, as ChromeDriver refuses it for a browser it did not start.
        /// </para>
        /// <para>
        /// For the same reason the browser keeps its tabs, cookies and cache
        /// from one test to the next. Each attach moves to a new tab, closes
        /// the others and clears the cookies and the cache, so a test starts
        /// as it would in a browser started for it.
        /// </para>
        /// </remarks>
        /// <param name="options">Options for the driver.</param>
        /// <returns>A driver, which the caller quits.</returns>
        public static WebDriver CreateCloak(ChromeOptions options) =>
            CreateCloak(Environment.GetEnvironmentVariable, options);

        /// <summary>
        /// As <see cref="CreateCloak(ChromeOptions)"/>, reading the variables
        /// through <paramref name="getVariable"/> so the rules can be tested
        /// without changing the environment of the whole run.
        /// </summary>
        /// <param name="getVariable">Reads an environment variable.</param>
        /// <param name="options">Options for the driver.</param>
        /// <returns>A driver, which the caller quits.</returns>
        public static WebDriver CreateCloak(
            Func<string, string> getVariable, ChromeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            var address = CloakDebuggerAddress(getVariable);
            if (address == null)
            {
                throw new WebDriverException(CloakNotConfiguredMessage);
            }
            var version = CloakMajorVersion(ReadCloakVersion(address));
            options.DebuggerAddress = address;
            var driverPath = FindNamedDriver(
                getVariable, CloakDriverVariable, ChromeDriverNames);
            ChromeDriver driver;
            try
            {
                driver = new ChromeDriver(
                    ChromeDriverService.CreateDefaultService(
                        driverPath ?? FetchChromeDriver(version)),
                    options);
            }
            catch (Exception e)
            {
                throw new WebDriverException(
                    $"Could not attach a driver to CloakBrowser at {address}, "
                    + $"the address {CloakDebuggerAddressVariable} names. It "
                    + $"is Chromium {version} and needs chromedriver "
                    + $"{version}, which was "
                    + (driverPath != null
                        ? $"taken from {driverPath}"
                        : "left to Selenium Manager to fetch")
                    + $". Set {CloakDriverVariable} to a chromedriver "
                    + $"{version} to name one. Selenium said: {e.Message}",
                    e);
            }
            try
            {
                StartClean(driver);
            }
            catch
            {
                driver.Quit();
                throw;
            }
            return driver;
        }

        /// <summary>
        /// The address CLOAK_DEBUGGER_ADDRESS names, or null when it is unset
        /// or empty, which is what a CI script that exports an unset variable
        /// produces.
        /// </summary>
        /// <param name="getVariable">Reads an environment variable.</param>
        /// <returns>The address as host:port, or null.</returns>
        public static string CloakDebuggerAddress(
            Func<string, string> getVariable)
        {
            if (getVariable == null)
            {
                throw new ArgumentNullException(nameof(getVariable));
            }
            var address = getVariable(CloakDebuggerAddressVariable);
            return string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        }

        /// <summary>
        /// The major version of the browser, read from the document its
        /// DevTools port serves at /json/version, where the Browser field
        /// reads like Chrome/146.0.7680.177.
        /// </summary>
        /// <param name="versionDocument">Body of /json/version.</param>
        /// <returns>The major version, for example 146.</returns>
        public static string CloakMajorVersion(string versionDocument)
        {
            string browser = null;
            try
            {
                using var document = JsonDocument.Parse(versionDocument);
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty(
                        "Browser", out var field)
                    && field.ValueKind == JsonValueKind.String)
                {
                    browser = field.GetString();
                }
            }
            catch (Exception e) when (
                e is JsonException || e is ArgumentNullException)
            {
                // Reported below, with what was received.
            }
            var slash = browser?.IndexOf('/') ?? -1;
            var major = slash < 0
                ? null
                : browser.Substring(slash + 1).Split('.')[0];
            if (string.IsNullOrEmpty(major)
                || int.TryParse(major, out _) == false)
            {
                throw new WebDriverException(
                    $"The address {CloakDebuggerAddressVariable} names did "
                    + "not answer /json/version with a browser version, so "
                    + "it is not the DevTools port of a browser. It "
                    + $"answered: {versionDocument}");
            }
            return major;
        }

        /// <summary>
        /// Body of /json/version from the browser at
        /// <paramref name="address"/>. The first request is what makes the
        /// container start the browser.
        /// </summary>
        /// <param name="address">Address as host:port.</param>
        /// <returns>The document, as JSON.</returns>
        private static string ReadCloakVersion(string address)
        {
            try
            {
                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(CloakVersionTimeoutSeconds),
                };
                return client.GetStringAsync(
                    new Uri($"http://{address}/json/version"))
                    .GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                throw new WebDriverException(
                    $"No browser answered at {address}, the address "
                    + $"{CloakDebuggerAddressVariable} names, which has to "
                    + "be the host and port of a running CloakBrowser, for "
                    + "example 127.0.0.1:9222. The README says how to start "
                    + $"one. The request said: {e.Message}",
                    e);
            }
        }

        /// <summary>
        /// Path to a chromedriver for Chrome of the given major version,
        /// which Selenium Manager fetches unless it already holds one.
        /// </summary>
        /// <remarks>
        /// Selenium Manager is asked directly, and only the driver it finds
        /// is used. Left to itself, Selenium also wants a Chrome on this
        /// machine, and fails on a machine that has none when
        /// SE_AVOID_BROWSER_DOWNLOAD stops it downloading one.
        /// </remarks>
        /// <param name="version">Major version of the browser.</param>
        /// <returns>Path to the driver.</returns>
        private static string FetchChromeDriver(string version)
        {
            var found = SeleniumManager.DiscoverBrowserAsync(
                "chrome",
                new BrowserDiscoveryOptions { BrowserVersion = version },
                CancellationToken.None).GetAwaiter().GetResult();
            return found.DriverPath;
        }

        /// <summary>
        /// Leaves an attached browser on one new tab with no cookies and an
        /// empty cache. Session storage belongs to a tab, so the new tab
        /// clears that too.
        /// </summary>
        /// <param name="driver">A driver attached to the browser.</param>
        private static void StartClean(ChromeDriver driver)
        {
            var stale = driver.WindowHandles;
            driver.SwitchTo().NewWindow(WindowType.Tab);
            var fresh = driver.CurrentWindowHandle;
            foreach (var handle in stale)
            {
                driver.SwitchTo().Window(handle);
                driver.Close();
            }
            driver.SwitchTo().Window(fresh);
            var none = new Dictionary<string, object>();
            driver.ExecuteCdpCommand("Network.clearBrowserCookies", none);
            driver.ExecuteCdpCommand("Network.clearBrowserCache", none);
        }

        /// <summary>
        /// Runs <paramref name="create"/>, which leaves Selenium Manager to
        /// fetch a driver, and on failure says what was looked for here first,
        /// so the reason reads as a missing browser rather than as a stack
        /// trace about a helper program.
        /// </summary>
        /// <param name="create">Creates the driver.</param>
        /// <param name="browser">Name of the browser, for the message.</param>
        /// <param name="variable">Variable that would name the driver.</param>
        /// <param name="names">File names looked for on the path.</param>
        /// <returns>A driver, which the caller quits.</returns>
        private static WebDriver Fetched(
            Func<WebDriver> create,
            string browser,
            string variable,
            IEnumerable<string> names)
        {
            try
            {
                return create();
            }
            catch (Exception e)
            {
                throw new WebDriverException(
                    $"No {browser} driver on this machine, so Selenium "
                    + "Manager was left to fetch one, and it could not. "
                    + $"Looked at {variable}, and for "
                    + $"{string.Join(", ", names)} on the path. This machine "
                    + $"is {RuntimeInformation.RuntimeIdentifier}. Set "
                    + $"{variable} to a driver, or {SeleniumUrlVariable} to a "
                    + $"Selenium that has {browser}. Selenium said: "
                    + e.Message,
                    e);
            }
        }

        /// <summary>
        /// Creates a driver on a remote Selenium when SELENIUM_URL is set.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        /// <param name="driver">The driver created, or null.</param>
        /// <returns>True when SELENIUM_URL is set.</returns>
        private static bool TryCreateRemote(
            DriverOptions options, out WebDriver driver)
        {
            if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
            {
                ExternalSeleniumHelper.AddExternalSeleniumArguments(options);
                driver = new RemoteWebDriver(new Uri(seleniumUrl), options);
                return true;
            }
            driver = null;
            return false;
        }

        /// <summary>
        /// Path to a driver this machine provides, or null when it provides
        /// none.
        /// </summary>
        /// <param name="variable">Variable that names the driver.</param>
        /// <param name="names">File names to look for on the path.</param>
        /// <returns>A path, or null.</returns>
        public static string FindDriver(
            string variable, IEnumerable<string> names) =>
            FindDriver(Environment.GetEnvironmentVariable, variable, names);

        /// <summary>
        /// As <see cref="FindDriver(string, IEnumerable{string})"/>, reading
        /// the variables through <paramref name="getVariable"/> so the rules
        /// can be tested without changing the environment of the whole run.
        /// </summary>
        /// <param name="getVariable">Reads an environment variable.</param>
        /// <param name="variable">Variable that names the driver.</param>
        /// <param name="names">File names to look for on the path.</param>
        /// <returns>A path, or null.</returns>
        public static string FindDriver(
            Func<string, string> getVariable,
            string variable,
            IEnumerable<string> names)
        {
            return FindNamedDriver(getVariable, variable, names)
                ?? FindOnPath(getVariable, names);
        }

        /// <summary>
        /// Path to the driver <paramref name="variable"/> names, as the driver
        /// itself or the directory holding it, or null. The path is not
        /// searched, which suits a driver that must not be the one the
        /// machine keeps for its own browser.
        /// </summary>
        /// <param name="getVariable">Reads an environment variable.</param>
        /// <param name="variable">Variable that names the driver.</param>
        /// <param name="names">File names to look for in a directory.</param>
        /// <returns>A path, or null.</returns>
        public static string FindNamedDriver(
            Func<string, string> getVariable,
            string variable,
            IEnumerable<string> names)
        {
            if (getVariable == null)
            {
                throw new ArgumentNullException(nameof(getVariable));
            }
            var configured = getVariable(variable);
            if (string.IsNullOrEmpty(configured))
            {
                return null;
            }
            return File.Exists(configured)
                ? configured
                : FindInDirectory(configured, names);
        }

        /// <summary>
        /// Points the options at a browser this machine provides, leaving them
        /// alone when it provides none, so Selenium Manager still fetches one.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        /// <param name="variable">Variable that names the browser.</param>
        /// <param name="names">File names to look for on the path.</param>
        private static void SetBinaryLocation(
            DriverOptions options, string variable, IEnumerable<string> names)
        {
            var configured = Environment.GetEnvironmentVariable(variable);
            var path = string.IsNullOrEmpty(configured) == false
                && File.Exists(configured)
                    ? configured
                    : FindOnPath(Environment.GetEnvironmentVariable, names);
            if (path != null)
            {
                options.BinaryLocation = path;
            }
        }

        /// <summary>
        /// The first of <paramref name="names"/> found in a directory named by
        /// PATH, or null.
        /// </summary>
        /// <param name="getVariable">Reads an environment variable.</param>
        /// <param name="names">File names to look for.</param>
        /// <returns>A path, or null.</returns>
        private static string FindOnPath(
            Func<string, string> getVariable, IEnumerable<string> names)
        {
            var path = getVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            foreach (var directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }
                var found = FindInDirectory(directory, names);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>
        /// The first of <paramref name="names"/> present in
        /// <paramref name="directory"/>, or null.
        /// </summary>
        /// <param name="directory">Directory to look in.</param>
        /// <param name="names">File names to look for.</param>
        /// <returns>A path, or null.</returns>
        private static string FindInDirectory(
            string directory, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(directory, name);
                }
                catch (ArgumentException)
                {
                    // A PATH entry holding characters a path cannot.
                    return null;
                }
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
