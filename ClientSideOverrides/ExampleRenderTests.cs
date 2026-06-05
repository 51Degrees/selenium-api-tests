using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Examples;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.ClientSideOverrides
{
    /// <summary>
    /// Loads a real example app the way an ordinary visitor would — plain
    /// desktop Chrome, no mobile emulation and no header-injecting proxy — and
    /// checks that the page the example renders server-side actually contains a
    /// real device-detection result. This is the "does the example work at all"
    /// counterpart to <see cref="ExampleClientSideOverrideTests"/>, which only
    /// reads the client-side <c>fod</c> object.
    /// </summary>
    [TestClass]
    public class ExampleRenderTests
    {
        private const int PageLoadTimeoutSeconds = 60;

        // A real desktop Chrome user agent, so the example detects a genuine
        // device rather than an unknown one.
        private const string DesktopChromeUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        // A device id is four hyphen-separated profile ids (hardware, platform,
        // browser, ...). Used to find the rendered id regardless of the label
        // each language's template puts in front of it.
        private static readonly Regex DeviceIdPattern =
            new Regex(@"\d+-\d+-\d+-\d+", RegexOptions.Compiled);

        private IExampleApp _example;
        private WebDriver _driver;

        /// <summary>Starts the example app and points it at the configured cloud.</summary>
        [TestInitialize]
        public async Task Init()
        {
            if (!ExampleApps.TryCreate(out var app, out var skipReason))
            {
                Assert.Inconclusive(skipReason);
                return;
            }
            _example = app;

            var cloudEndpoint = TestHelpers.GetActualRootUrl(TestInitialiser.CloudServerUrl);
            if (string.IsNullOrEmpty(cloudEndpoint))
            {
                Assert.Inconclusive(
                    "No external cloud configured. Set TEST_CONFIG_FILE to a json file " +
                    "with \"cloud_root_url\" (e.g. https://cloud.51degrees.com/).");
                return;
            }

            await _example.StartAsync(
                new ExampleAppOptions(
                    Port: TestHelpers.GetRandomUnusedPort(),
                    CloudEndpoint: new Uri(cloudEndpoint),
                    ResourceKey: TestResourceKey.PaidResourceKey,
                    ExtraEnv: new Dictionary<string, string>()),
                CancellationToken.None);
        }

        /// <summary>
        /// The example's server-rendered page shows a real detection result for
        /// a normal desktop browser: the browser is identified as Chrome and the
        /// device id is a genuine profile (not the all-zero "unknown" id).
        /// </summary>
        [TestMethod]
        public void Example_RendersRealDetectionResult()
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AcceptInsecureCertificates = true;
            chromeOptions.AddArgument("--headless");
            chromeOptions.AddArgument($"--user-agent={DesktopChromeUserAgent}");

            if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
            {
                ExternalSeleniumHelper.AddExternalSeleniumArguments(chromeOptions);
                _driver = new RemoteWebDriver(new Uri(seleniumUrl), chromeOptions);
            }
            else
            {
                _driver = new ChromeDriver(chromeOptions);
            }

            _driver.Navigate().GoToUrl(_example.BaseUrl);

            // java's example page doesn't render a device id, so skip that check.
            var isJava = string.Equals(
                ExampleApps.SelectedLang, "java", StringComparison.OrdinalIgnoreCase);

            // The detection table is rendered server-side on the first response,
            // but wait so a slow example (cold start) doesn't cause a flake.
            new WebDriverWait(_driver, TimeSpan.FromSeconds(PageLoadTimeoutSeconds)).Until(
                d => isJava
                    ? d.PageSource.Contains("Chrome")
                    : DeviceIdPattern.IsMatch(d.PageSource));

            var pageSource = _driver.PageSource;

            StringAssert.Contains(
                pageSource, "Chrome",
                "rendered page does not mention the detected browser (Chrome)");

            if (!isJava)
            {
                var deviceId = DeviceIdPattern.Match(pageSource).Value;
                Assert.IsFalse(
                    IsAllZero(deviceId),
                    $"device id '{deviceId}' is all zeros — desktop hardware was not detected");
            }
        }

        /// <summary>True if every component of a hyphen-joined id is zero.</summary>
        private static bool IsAllZero(string deviceId)
        {
            foreach (var part in deviceId.Split('-'))
            {
                if (part != "0")
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Quits the browser and stops the example app.</summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            _driver?.Quit();
            _driver?.Dispose();
            if (_example != null)
            {
                await _example.DisposeAsync();
            }
        }
    }
}
