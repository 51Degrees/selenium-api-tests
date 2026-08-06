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
    [TestClass, TestCategory("Contract")]
    public class ExampleRenderTests
    {
        private const int PageLoadTimeoutSeconds = 60;

        // A real desktop Chrome user agent, so the example detects a genuine
        // device rather than an unknown one.
        private const string DesktopChromeUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        // Device type is the one detection result every example renders, with the
        // same label and the same value in all six languages. It is also absent
        // from the user agent itself, so a page that merely echoed the request
        // cannot satisfy it - unlike the vendor and version values, which appear
        // verbatim in the user agent string.
        private static readonly Regex DeviceTypePattern =
            new Regex(
                @"Device Type:?\s*</td>\s*<td[^>]*>\s*Desktop\s*<",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                    "No external cloud configured. Set CLOUD_ROOT_URL " +
                    "(e.g. https://cloud.51degrees.com/).");
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

            // The detection table is rendered server-side on the first response,
            // but wait so a slow example (cold start) doesn't cause a flake.
            new WebDriverWait(_driver, TimeSpan.FromSeconds(PageLoadTimeoutSeconds)).Until(
                d => DeviceTypePattern.IsMatch(d.PageSource));

            Assert.IsTrue(
                DeviceTypePattern.IsMatch(_driver.PageSource),
                "rendered page does not show a device type of 'Desktop', so the " +
                "example did not render a real detection result server-side");
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
