using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
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
            if (getVariable == null)
            {
                throw new ArgumentNullException(nameof(getVariable));
            }
            var configured = getVariable(variable);
            if (string.IsNullOrEmpty(configured) == false)
            {
                if (File.Exists(configured))
                {
                    return configured;
                }
                var inDirectory = FindInDirectory(configured, names);
                if (inDirectory != null)
                {
                    return inDirectory;
                }
            }
            return FindOnPath(getVariable, names);
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
