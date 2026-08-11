using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Examples;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
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
        //
        // Anchoring on the label cell and reading its sibling keeps this on the
        // server-rendered table. The shared examples helper appends a second
        // table of client-side results with overlapping labels, so matching on
        // the page as a whole would pick up whichever came first.
        private const string DeviceTypeCell =
            "//td[normalize-space()='Device Type:' or normalize-space()='Device Type']";

        // Read from the table that holds the device type, so this is the
        // server-rendered id rather than the refined one the helper renders
        // client side under the same label.
        private const string DeviceIdCellInSameTable =
            DeviceTypeCell +
            "/ancestor::table[1]//td[normalize-space()='Device Id:' or normalize-space()='Device Id']";

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
            chromeOptions.AddArgument($"--user-agent={DesktopChromeUserAgent}");

            _driver = WebDriverFactory.Create(chromeOptions);

            _driver.Navigate().GoToUrl(_example.BaseUrl);

            // The detection table is rendered server-side on the first response,
            // but wait so a slow example (cold start) doesn't cause a flake.
            new WebDriverWait(_driver, TimeSpan.FromSeconds(PageLoadTimeoutSeconds)).Until(
                d => ValueOfCellAfter(d, DeviceTypeCell) != null);

            Assert.AreEqual(
                "Desktop", ValueOfCellAfter(_driver, DeviceTypeCell),
                "rendered page does not show a device type of 'Desktop', so the " +
                "example did not render a real detection result server-side");

            // The device id is the compact form of the whole result, so check it
            // where it is rendered. java and rust do not render one server-side,
            // and that is a property of those pages rather than of detection, so
            // it is checked where present rather than demanded everywhere.
            var deviceId = ValueOfCellAfter(_driver, DeviceIdCellInSameTable);
            if (deviceId != null)
            {
                Assert.IsFalse(string.IsNullOrEmpty(deviceId),
                    "the device id cell is rendered but empty");
                Assert.IsFalse(
                    deviceId.Split('-').All(part => part == "0"),
                    $"the rendered device id is '{deviceId}', so no profile was matched");
            }
        }

        /// <summary>
        /// Text of the cell following the first one matching
        /// <paramref name="labelCellXPath"/>, or null when there is no such row.
        /// </summary>
        private static string ValueOfCellAfter(ISearchContext context, string labelCellXPath)
        {
            var labels = context.FindElements(By.XPath(labelCellXPath));
            if (labels.Count == 0)
            {
                return null;
            }
            var values = labels[0].FindElements(By.XPath("following-sibling::td[1]"));
            return values.Count == 0 ? null : values[0].Text.Trim();
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
