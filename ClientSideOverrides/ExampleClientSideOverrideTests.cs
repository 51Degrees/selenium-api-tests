using System;
using System.Collections.Generic;
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
using OpenQA.Selenium.Support.UI;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.ClientSideOverrides
{
    /// <summary>
    /// Runs the client-side-override checks against a real example app that serves
    /// 51Degrees.core.js from its own pipeline and talks to the cloud.
    /// </summary>
    [TestClass, TestCategory("Contract")]
    public class ExampleClientSideOverrideTests
    {
        private const int JavaScriptTimeout = 60;
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
        /// The example serves core.js from its own pipeline and client-side JS
        /// evidence (screen size) flows back into detection.
        /// </summary>
        [TestMethod]
        [DataRow(375, 667, 2.0,
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1")]
        [DataRow(414, 736, 3.0,
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1")]
        public void Example_ServesCoreJs_AndClientSideOverridesFlow(
            long width, long height, double pixelRatio, string userAgent)
        {
            var chromeOptions = new ChromeOptions();
            // disable UA client hints so the emulated user agent is used
            chromeOptions.AddArgument("--disable-features=UserAgentClientHint");
            chromeOptions.EnableMobileEmulation(new ChromiumMobileEmulationDeviceSettings
            {
                Width = width,
                Height = height,
                PixelRatio = pixelRatio,
                UserAgent = userAgent,
            });

            _driver = WebDriverFactory.Create(chromeOptions);

            _driver.Navigate().GoToUrl(_proxyUrl);
            IJavaScriptExecutor js = _driver;

            new WebDriverWait(_driver, TimeSpan.FromSeconds(JavaScriptTimeout)).Until(
                _ => "function".Equals(
                    js.ExecuteScript("return (typeof fod !== 'undefined' && fod) ? typeof fod.complete : 'none';")));

            js.ExecuteScript(@"
                window.__t = 'loading';
                window.__spw = 0; window.__sph = 0; window.__did = 'loading';
                window.__sw = window.screen.width; window.__sh = window.screen.height;
                fod.complete(function (data) {
                    window.__spw = data.device['screenpixelswidth'];
                    window.__sph = data.device['screenpixelsheight'];
                    window.__did = data.device['deviceid'];
                    window.__t = 'complete';
                });");

            new WebDriverWait(_driver, TimeSpan.FromSeconds(JavaScriptTimeout)).Until(
                _ => "complete".Equals(js.ExecuteScript("return window.__t")) &&
                     !"loading".Equals(js.ExecuteScript("return window.__did")));

            var screenWidth = Convert.ToInt64(js.ExecuteScript("return window.__sw"));
            var screenHeight = Convert.ToInt64(js.ExecuteScript("return window.__sh"));
            var screenPixelWidth = Convert.ToInt64(js.ExecuteScript("return window.__spw"));
            var screenPixelHeight = Convert.ToInt64(js.ExecuteScript("return window.__sph"));
            var deviceId = js.ExecuteScript("return window.__did")?.ToString();

            Assert.AreEqual(width, screenWidth, "emulated screen width does not match");
            Assert.AreEqual(height, screenHeight, "emulated screen height does not match");

            Assert.IsTrue(screenPixelWidth > 0, "screenpixelswidth override did not resolve");
            Assert.IsTrue(screenPixelHeight > 0, "screenpixelsheight override did not resolve");

            Assert.IsFalse(string.IsNullOrEmpty(deviceId), "device id was not resolved");
            Assert.AreNotEqual("loading", deviceId, "device id was not resolved");
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
