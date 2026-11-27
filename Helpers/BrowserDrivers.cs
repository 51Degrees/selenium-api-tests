using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
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

        /// <summary>
        /// Seconds the first start of a browser in a run may take. A
        /// browser's first start on a fresh runner can take minutes, where
        /// the driver allows each command 60 seconds, so the first start is
        /// made by <see cref="StartOnce"/> with this limit instead.
        /// </summary>
        public const int FirstStartTimeoutSeconds = 300;

        /// <summary>
        /// Seconds each command of every local Chrome and Firefox session, and
        /// of every remote session, may take, its start included. A later
        /// start can take as long as a first, so every session has the first
        /// start's limit in place of the driver's 60 seconds.
        /// </summary>
        public const int SessionTimeoutSeconds = FirstStartTimeoutSeconds;

        private static readonly TimeSpan _sessionTimeout =
            TimeSpan.FromSeconds(SessionTimeoutSeconds);

        /// <summary>
        /// Arguments every Chrome and Edge is given. --no-sandbox turns off
        /// the browser's sandbox, without which Chrome exits as it starts on
        /// some runners, and --disable-dev-shm-usage makes the browser write
        /// its shared memory files to a temporary directory rather than
        /// /dev/shm, which is too small in some virtual machines and
        /// containers.
        /// </summary>
        public static readonly IReadOnlyList<string> ChromeArguments =
            new[] { "--no-sandbox", "--disable-dev-shm-usage" };

        /// <summary>
        /// The argument that starts Chrome, Edge and Firefox without a window.
        /// </summary>
        public const string HeadlessArgument = "--headless";

        /// <summary>Page the first start of a browser loads.</summary>
        private const string FirstStartPage = "about:blank";

        private static readonly object _firstStartLock = new object();

        private static readonly Dictionary<string, string> _firstStarts =
            new Dictionary<string, string>();

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
        /// Gives options what every browser in the suite has. Every browser
        /// runs headless and accepts insecure certificates, and Chrome and
        /// Edge get <see cref="ChromeArguments"/>. A headless argument the
        /// caller chose, such as --headless=new, is kept and not joined by a
        /// second, and calling this again adds nothing. Selenium offers no way
        /// to read back a Firefox argument, so Firefox is given
        /// <see cref="HeadlessArgument"/> whatever it holds, and a second
        /// headless argument there changes nothing.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        public static void Prepare(DriverOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            options.AcceptInsecureCertificates = true;
            switch (options)
            {
                case ChromiumOptions chromium:
                    if (chromium.Arguments.Any(a => a.StartsWith(
                        HeadlessArgument, StringComparison.Ordinal)) == false)
                    {
                        chromium.AddArgument(HeadlessArgument);
                    }
                    AddChromeArguments(chromium);
                    break;
                case FirefoxOptions firefox:
                    firefox.AddArgument(HeadlessArgument);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(options),
                        $"{options.GetType().Name} is not a browser these "
                        + "tests use.");
            }
        }

        /// <summary>
        /// Creates a Chrome or Chromium driver, with the options given what
        /// <see cref="Prepare"/> gives.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        /// <returns>A driver, which the caller quits.</returns>
        public static WebDriver CreateChrome(ChromeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            Prepare(options);
            if (TryCreateRemote(options, out var remote))
            {
                return remote;
            }
            SetBinaryLocation(options, ChromeBinaryVariable, _chromeBinaryNames);
            var driverPath = FindDriver(ChromeDriverVariable, ChromeDriverNames);
            StartOnce("Chrome", timeout => Visit(new ChromeDriver(
                ChromeService(driverPath), options, timeout)));
            if (driverPath != null)
            {
                return new ChromeDriver(
                    ChromeService(driverPath), options, _sessionTimeout);
            }
            return Fetched(
                () => new ChromeDriver(
                    ChromeService(null), options, _sessionTimeout),
                "Chrome", ChromeDriverVariable, ChromeDriverNames);
        }

        /// <summary>
        /// Adds each of <see cref="ChromeArguments"/> that the Chrome or Edge
        /// options do not already carry, so an argument a caller set is not
        /// given twice.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        public static void AddChromeArguments(ChromiumOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            foreach (var argument in ChromeArguments)
            {
                if (options.Arguments.Contains(argument) == false)
                {
                    options.AddArgument(argument);
                }
            }
        }

        /// <summary>
        /// A Chrome driver service that writes no verbose or appended log,
        /// prints no start up banner and opens no console window.
        /// </summary>
        /// <param name="driverPath">
        /// The driver this machine provides, or null to leave Selenium Manager
        /// to fetch one.
        /// </param>
        /// <returns>The service, which the driver created with it owns.</returns>
        internal static ChromeDriverService ChromeService(string driverPath)
        {
            var service = driverPath != null
                ? ChromeDriverService.CreateDefaultService(driverPath)
                : ChromeDriverService.CreateDefaultService();
            service.EnableVerboseLogging = false;
            service.EnableAppendLog = false;
            Quieten(service);
            return service;
        }

        /// <summary>
        /// An Edge driver service as quiet as <see cref="ChromeService"/>.
        /// </summary>
        /// <param name="driverPath">
        /// The driver this machine provides, or null to leave Selenium Manager
        /// to fetch one.
        /// </param>
        /// <returns>The service, which the driver created with it owns.</returns>
        internal static EdgeDriverService EdgeService(string driverPath)
        {
            var service = driverPath != null
                ? EdgeDriverService.CreateDefaultService(driverPath)
                : EdgeDriverService.CreateDefaultService();
            service.EnableVerboseLogging = false;
            service.EnableAppendLog = false;
            Quieten(service);
            return service;
        }

        /// <summary>
        /// A Firefox driver service that prints no start up banner and opens
        /// no console window.
        /// </summary>
        /// <param name="driverPath">
        /// The driver this machine provides, or null to leave Selenium Manager
        /// to fetch one.
        /// </param>
        /// <returns>The service, which the driver created with it owns.</returns>
        internal static FirefoxDriverService FirefoxService(string driverPath)
        {
            var service = driverPath != null
                ? FirefoxDriverService.CreateDefaultService(driverPath)
                : FirefoxDriverService.CreateDefaultService();
            Quieten(service);
            return service;
        }

        private static void Quieten(DriverService service)
        {
            service.SuppressInitialDiagnosticInformation = true;
            service.HideCommandPromptWindow = true;
        }

        /// <summary>
        /// Creates a Firefox driver, with the options given what
        /// <see cref="Prepare"/> gives.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        /// <returns>A driver, which the caller quits.</returns>
        public static WebDriver CreateFirefox(FirefoxOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            Prepare(options);
            if (TryCreateRemote(options, out var remote))
            {
                return remote;
            }
            SetBinaryLocation(
                options, FirefoxBinaryVariable, _firefoxBinaryNames);
            var driverPath = FindDriver(GeckoDriverVariable, GeckoDriverNames);
            StartOnce("Firefox", timeout => Visit(new FirefoxDriver(
                FirefoxService(driverPath), options, timeout)));
            if (driverPath != null)
            {
                return new FirefoxDriver(
                    FirefoxService(driverPath), options, _sessionTimeout);
            }
            return Fetched(
                () => new FirefoxDriver(
                    FirefoxService(null), options, _sessionTimeout),
                "Firefox", GeckoDriverVariable, GeckoDriverNames);
        }

        /// <summary>
        /// Creates an Edge driver, with the options given what
        /// <see cref="Prepare"/> gives, a quiet driver service and
        /// <see cref="SessionTimeoutSeconds"/> for each command. Edge is
        /// started once first, as Chrome and Firefox are: on a fresh
        /// ubuntu-26.04 runner its first session's first page load took more
        /// than the 300 seconds a command may take, failing the first test of
        /// EdgeTests in its initialisation (cloud CI, 26 and 27 November
        /// 2026), while every later load was quick.
        /// </summary>
        /// <param name="options">Options for the browser.</param>
        /// <returns>A driver, which the caller quits.</returns>
        public static WebDriver CreateEdge(EdgeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            Prepare(options);
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
            StartOnce("Edge", timeout => Visit(new EdgeDriver(
                EdgeService(driverPath), options, timeout)));
            if (driverPath != null)
            {
                return new EdgeDriver(
                    EdgeService(driverPath), options, _sessionTimeout);
            }
            return Fetched(
                () => new EdgeDriver(
                    EdgeService(null), options, _sessionTimeout),
                "Edge", EdgeDriverVariable, EdgeDriverNames);
        }

        /// <summary>
        /// The line written at the first start of each browser in this run,
        /// by the browser's name.
        /// </summary>
        public static IReadOnlyDictionary<string, string> FirstStarts
        {
            get
            {
                lock (_firstStartLock)
                {
                    return new Dictionary<string, string>(_firstStarts);
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="start"/> the first time a browser is asked
        /// for in this run, and never again, so that the browser's slow first
        /// start is made before the first test that uses it and not inside
        /// one of that test's commands. Writes how long it took to the output
        /// of the test that asked. Whatever stops the start is written and
        /// not thrown, and the test then meets it when it starts the browser
        /// itself.
        /// </summary>
        /// <param name="browser">Name of the browser.</param>
        /// <param name="start">
        /// Starts the browser, loads a page and quits, with the time limit
        /// for each command it is given.
        /// </param>
        public static void StartOnce(string browser, Action<TimeSpan> start)
        {
            if (browser == null)
            {
                throw new ArgumentNullException(nameof(browser));
            }
            if (start == null)
            {
                throw new ArgumentNullException(nameof(start));
            }
            lock (_firstStartLock)
            {
                if (_firstStarts.ContainsKey(browser))
                {
                    return;
                }
                var watch = Stopwatch.StartNew();
                string line;
                try
                {
                    start(TimeSpan.FromSeconds(FirstStartTimeoutSeconds));
                    line = $"Started {browser} once before its first test "
                        + "in " + Seconds(watch) + " s.";
                }
                catch (Exception e)
                {
                    line = $"{browser} did not finish its first start in "
                        + Seconds(watch) + " s, so the test starts it as it "
                        + "would have. " + FirstLine(e.Message);
                }
                _firstStarts[browser] = line;
                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Loads a page in <paramref name="driver"/> and quits it.
        /// </summary>
        /// <param name="driver">A driver just created.</param>
        private static void Visit(WebDriver driver)
        {
            try
            {
                driver.Navigate().GoToUrl(FirstStartPage);
            }
            finally
            {
                driver.Quit();
            }
        }

        /// <summary>Seconds on the watch, to one decimal place.</summary>
        /// <param name="watch">A running watch.</param>
        /// <returns>The seconds, written the same in every culture.</returns>
        private static string Seconds(Stopwatch watch) =>
            watch.Elapsed.TotalSeconds.ToString(
                "0.0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// The first line of <paramref name="text"/>, which for a driver's
        /// message is the reason without the details that follow it.
        /// </summary>
        /// <param name="text">Text of one or more lines.</param>
        /// <returns>The first line, or an empty string.</returns>
        private static string FirstLine(string text) =>
            (text ?? string.Empty).Split('\n')[0].TrimEnd('\r');

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
                driver = CreateRemote(new Uri(seleniumUrl), options);
                return true;
            }
            driver = null;
            return false;
        }

        /// <summary>
        /// Creates a session on the remote Selenium at <paramref name="url"/>,
        /// with <see cref="SessionTimeoutSeconds"/> for each command, the
        /// same as a local Chrome or Firefox session.
        /// </summary>
        /// <param name="url">The remote Selenium.</param>
        /// <param name="options">Options for the browser.</param>
        /// <returns>A driver, which the caller quits.</returns>
        internal static WebDriver CreateRemote(Uri url, DriverOptions options)
            => new RemoteWebDriver(url, options.ToCapabilities(), _sessionTimeout);

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
