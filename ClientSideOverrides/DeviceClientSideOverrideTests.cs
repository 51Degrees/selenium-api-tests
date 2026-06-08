using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestElements;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.ClientSideOverrides
{
    /// <summary>
    /// Test functionality of Client-Side Overrides for the Device-Detection feature.
    /// </summary>
    [TestClass, TestCategory("CloudInternal")]
    public class DeviceClientSideOverrideTests
    {
        private string ClientServerUrl;
        private CancellationTokenSource clientServerTokenSource;
        private HttpListener clientServer;
        private WebDriver driver;
        private const int JavaScriptTimeout = 60; // in seconds

        /// <summary>
        /// Initializes the test.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            var pageData = @"
<!DOCTYPE>
<html>
    <head>
        <title>Test Page</title>
        <script async src=""api/v4/" + TestResourceKey.PaidJavaScriptEndpoint + @"""></script>
    </head>
    <body>
        <script>
            var test = 'loading';
            var spw = 0;
            var sph = 0;
            var deviceid = 'loading';
            var browserScreenWidth = 0;
            var browserScreenHeight = 0;

            window.onload = function() {
                fod.complete(function(data) {
                    spw = data.device['screenpixelswidth'];
                    sph = data.device['screenpixelsheight'];
                    deviceid = data.device['deviceid'];
                    browserScreenWidth = window.screen.width;
                    browserScreenHeight = window.screen.height;
                    test = 'complete';
                });
            }
        </script>
    </body>
</html>
";

            // Start the client server. It serves the page on /, and proxies
            // /api/* to the cloud, mimicking the production setup where the
            // customer's site talks to the cloud server-to-server.
            ClientServerUrl = $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            clientServerTokenSource = new CancellationTokenSource();
            var token = clientServerTokenSource.Token;

            // Opt this origin out of UA Client Hints. Selenium's
            // ChromiumMobileEmulationDeviceSettings overrides
            // navigator.userAgent but does not override the low-entropy
            // hints (Sec-CH-UA, Sec-CH-UA-Mobile, Sec-CH-UA-Platform), so
            // on Linux CI runners the browser leaks "Chromium 147 / ?0 /
            // Linux" alongside an emulated iOS user agent. The cloud then
            // sees contradictory evidence (UA says iOS, hints say desktop
            // Linux) and falls back to an Unknown profile whose JS-property
            // bodies are empty, so the bundle silently skips the
            // screen-pixel cookie writes this test asserts on.
            //
            // Permissions-Policy: ch-ua=(), ch-ua-mobile=(),
            // ch-ua-platform=() instructs the browser to stop sending those
            // hints on subsequent same-origin requests (the bundle fetch
            // and its refetch), matching real iOS Safari (which sends none).
            // Scoped to this test only — other Selenium tests still see the
            // browser's natural Sec-CH-UA-* headers.
            var pageHeaders = new Dictionary<string, string>
            {
                ["Permissions-Policy"] = "ch-ua=(), ch-ua-mobile=(), ch-ua-platform=()",
            };

            clientServer = TestHelpers.ProxyingListener(
                    ClientServerUrl,
                    TestHelpers.GetActualRootUrl(TestInitialiser.CloudServerUrl),
                    pageData,
                    token,
                    pageHeaders)
                .Listener;
        }

        /// <summary>
        /// Test that client-side overrides are still populated under headless
        /// Chrome when using mobile emulation.
        /// </summary>
        [TestMethod]
        [DataRow(
            375,
            667,
            2.0,
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
            "98237")]
        [DataRow(
            414,
            736,
            3.0,
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
            "98238")]
        public void JavaScript_ClientSideOverrides(
            long width,
            long height,
            double pixelRatio,
            string userAgent,
            string expectedHardwareId)
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AcceptInsecureCertificates = true;
            chromeOptions.AddArgument("--headless");
            chromeOptions.EnableMobileEmulation(
                new ChromiumMobileEmulationDeviceSettings
                {
                    Width = width,
                    Height = height,
                    PixelRatio = pixelRatio,
                    UserAgent = userAgent,
                });

            if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
            {
                ExternalSeleniumHelper.AddExternalSeleniumArguments(chromeOptions);
                driver = new RemoteWebDriver(new Uri(seleniumUrl), chromeOptions);
            }
            else
            {
                driver = new ChromeDriver(chromeOptions);
            }
            driver.Manage().Cookies.DeleteAllCookies();

            driver.Navigate().GoToUrl(ClientServerUrl);

            IJavaScriptExecutor js = driver;
            new WebDriverWait(driver, TimeSpan.FromSeconds(JavaScriptTimeout)).Until(
                _ => "complete".Equals(js.ExecuteScript("return test")) &&
                     !"loading".Equals(js.ExecuteScript("return deviceid")));

            var screenWidth = Convert.ToInt64(js.ExecuteScript("return browserScreenWidth"));
            var screenHeight = Convert.ToInt64(js.ExecuteScript("return browserScreenHeight"));
            var screenPixelWidth = Convert.ToInt64(js.ExecuteScript("return spw"));
            var screenPixelHeight = Convert.ToInt64(js.ExecuteScript("return sph"));
            var deviceId = js.ExecuteScript("return deviceid")?.ToString();

            Assert.AreEqual(width, screenWidth, "emulated screen width does not match");
            Assert.AreEqual(height, screenHeight, "emulated screen height does not match");

            // The bundle stores JS-collected evidence in cookies when
            // CloudJavaScriptBuilderElement is configured with EnableCookies=true
            // (CI), and otherwise in sessionStorage. Look in both so the test
            // works in either configuration.
            var evidenceWidth = JsEvidenceHelper.Read(js, driver, "51D_ScreenPixelsWidth");
            var evidenceHeight = JsEvidenceHelper.Read(js, driver, "51D_ScreenPixelsHeight");
            Assert.IsNotNull(evidenceWidth, "51D_ScreenPixelsWidth was not captured by client-side JS (in cookie or sessionStorage)");
            Assert.IsNotNull(evidenceHeight, "51D_ScreenPixelsHeight was not captured by client-side JS (in cookie or sessionStorage)");
            Assert.AreEqual(evidenceWidth.Value, screenPixelWidth, "screenpixelswidth does not match the JS-captured 51D_ScreenPixelsWidth");
            Assert.AreEqual(evidenceHeight.Value, screenPixelHeight, "screenpixelsheight does not match the JS-captured 51D_ScreenPixelsHeight");
            Assert.AreEqual(
                expectedHardwareId,
                deviceId?.Split('-')[0],
                "hardware id returned by js does not match");
        }

        /// <summary>
        /// Cleans up after the test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            driver?.Quit();
            clientServerTokenSource?.Cancel();
            clientServer?.Stop();
            clientServer?.Close();
        }
    }
}
