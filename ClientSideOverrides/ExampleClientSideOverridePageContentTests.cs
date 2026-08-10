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
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.ClientSideOverrides
{
    /// <summary>
    /// Checks the client-side override round trip the way a visitor would see
    /// it: by reading the results the example renders onto its own page.
    /// <para>
    /// <see cref="ExampleClientSideOverrideTests"/> covers the same round trip
    /// by reading the values out of the flow data in script. That proves the
    /// data reached the browser; it does not prove the example put it in front
    /// of anyone. This reads the rendered table instead, so a page that
    /// resolves the evidence correctly but never displays it fails here.
    /// </para>
    /// </summary>
    [TestClass, TestCategory("Contract")]
    public class ExampleClientSideOverridePageContentTests
    {
        private const int RenderTimeoutSeconds = 60;

        // Labels come from the shared examples helper, which every example
        // vendors from the same release, so they are the same in all languages.
        private const string ScreenWidthLabel = "Screen width (pixels):";
        private const string ScreenHeightLabel = "Screen height (pixels):";
        private const string DeviceIdLabel = "Device Id:";

        private IExampleApp _example;
        private WebDriver _driver;
        private TestHelpers.ServerListener _proxy;
        private CancellationTokenSource _proxyTokenSource;
        private string _proxyUrl;

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

            // Front the example with a proxy that disables UA client hints, so they
            // don't override the emulated mobile user agent.
            _proxyTokenSource = new CancellationTokenSource();
            _proxyUrl = $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            _proxy = TestHelpers.ReverseProxyListener(
                _proxyUrl,
                _example.BaseUrl.ToString(),
                _proxyTokenSource.Token,
                new Dictionary<string, string>
                {
                    ["Permissions-Policy"] = "ch-ua=(), ch-ua-mobile=(), ch-ua-platform=()",
                });
        }

        /// <summary>
        /// The screen size the browser reports client side is resolved by the
        /// cloud and rendered onto the example's page.
        /// </summary>
        [TestMethod]
        [DataRow(375, 667, 2.0,
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1")]
        [DataRow(414, 736, 3.0,
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1")]
        public void Example_RendersClientSideOverridesOnThePage(
            long width, long height, double pixelRatio, string userAgent)
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AcceptInsecureCertificates = true;
            chromeOptions.AddArgument("--headless");
            // disable UA client hints so the emulated user agent is used
            chromeOptions.AddArgument("--disable-features=UserAgentClientHint");
            chromeOptions.EnableMobileEmulation(new ChromiumMobileEmulationDeviceSettings
            {
                Width = width,
                Height = height,
                PixelRatio = pixelRatio,
                UserAgent = userAgent,
            });

            if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
            {
                ExternalSeleniumHelper.AddExternalSeleniumArguments(chromeOptions);
                _driver = new RemoteWebDriver(new Uri(seleniumUrl), chromeOptions);
            }
            else
            {
                _driver = new ChromeDriver(chromeOptions);
            }

            _driver.Navigate().GoToUrl(_proxyUrl);

            var rendered = WaitForRenderedResults(width.ToString(), height.ToString());

            Assert.AreEqual(
                width.ToString(), rendered[ScreenWidthLabel],
                "the screen width rendered on the page does not match the emulated width");
            Assert.AreEqual(
                height.ToString(), rendered[ScreenHeightLabel],
                "the screen height rendered on the page does not match the emulated height");

            // The device id is only rendered by examples whose copy of the
            // shared helper is current. An example still carrying an older copy
            // is a stale vendored asset rather than a detection failure, so say
            // which it is instead of reporting a missing row as a bad result.
            if (!rendered.TryGetValue(DeviceIdLabel, out var deviceId))
            {
                Assert.Inconclusive(
                    $"The '{ExampleApps.SelectedLang}' example renders no '{DeviceIdLabel}' " +
                    "row, so its copy of the shared examples helper predates the row " +
                    $"being added. Rendered labels: [{string.Join(", ", rendered.Keys)}].");
                return;
            }

            Assert.IsFalse(string.IsNullOrEmpty(deviceId),
                "the device id rendered on the page is empty, so the client-side " +
                "results reached the page without a resolved device id");
            Assert.IsFalse(
                deviceId.Split('-').All(part => part == "0"),
                $"the device id rendered on the page is '{deviceId}', which means " +
                "no profile was matched");
        }

        /// <summary>
        /// Waits for the example's page to render the client-side results, then
        /// reads the label and value cells back out of it.
        /// </summary>
        /// <remarks>
        /// The example first renders the User-Agent-only (server-side) result,
        /// then a client-side callback overwrites it with the values resolved
        /// from the browser's evidence. Both carry the same labels and the
        /// server-side row is present from first paint, so waiting merely for a
        /// label to appear returns before the callback runs and reads the
        /// server-side value on slower machines. Wait until the rendered screen
        /// width and height match the emulated values instead - that is the
        /// signal that the client-side result has actually landed.
        /// </remarks>
        private Dictionary<string, string> WaitForRenderedResults(
            string expectedWidth, string expectedHeight)
        {
            var containerId = ExampleApps.ClientResultsElementId;
            var by = By.CssSelector($"#{containerId} tr");

            try
            {
                new WebDriverWait(_driver, TimeSpan.FromSeconds(RenderTimeoutSeconds))
                    .Until(d =>
                    {
                        var rows = ReadRows(d, by);
                        return rows.GetValueOrDefault(ScreenWidthLabel) == expectedWidth
                            && rows.GetValueOrDefault(ScreenHeightLabel) == expectedHeight;
                    });
            }
            catch (WebDriverTimeoutException)
            {
                var rows = ReadRows(_driver, by);
                Assert.Fail(
                    $"The '{ExampleApps.SelectedLang}' example did not render the " +
                    $"client-side screen size into '#{containerId}' within " +
                    $"{RenderTimeoutSeconds}s. Expected '{ScreenWidthLabel}' " +
                    $"'{expectedWidth}' and '{ScreenHeightLabel}' '{expectedHeight}', " +
                    $"but rendered [{string.Join(", ", rows.Select(r => $"{r.Key} '{r.Value}'"))}].");
            }

            return ReadRows(_driver, by);
        }

        /// <summary>
        /// Label to value for every two-cell row in the results container. Read
        /// through the browser's own rendering rather than from script, so what
        /// is asserted is what a visitor would see.
        /// </summary>
        private static Dictionary<string, string> ReadRows(ISearchContext context, By by)
        {
            var rows = new Dictionary<string, string>();
            foreach (var row in context.FindElements(by))
            {
                var cells = row.FindElements(By.TagName("td"));
                if (cells.Count >= 2)
                {
                    rows[cells[0].Text.Trim()] = cells[1].Text.Trim();
                }
            }
            return rows;
        }

        /// <summary>Quits the browser and stops the example app.</summary>
        [TestCleanup]
        public async Task Cleanup()
        {
            _driver?.Quit();
            _driver?.Dispose();
            _proxyTokenSource?.Cancel();
            _proxy?.Listener?.Stop();
            _proxy?.Listener?.Close();
            if (_example != null)
            {
                await _example.DisposeAsync();
            }
        }
    }
}
