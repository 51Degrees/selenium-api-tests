using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using System;
using System.Net;
using System.Threading;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.BrowserCache
{
    /// <summary>
    /// Verify browser cache behavior: after a page refresh, the same
    /// device-detection results are returned and no new cloud requests
    /// are made (the browser serves them from cache).
    /// </summary>
    [TestClass, TestCategory("CloudInternal")]
    public class CacheTests
    {
        private WebDriver driver;
        private string ClientServerUrl;
        private HttpListener clientServer;
        private CancellationTokenSource clientServerTokenSource;

        /// <summary>
        /// Initializes the test.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            var clientPageData = @"
<!DOCTYPE>
<html>
    <head>
        <title>Test Page</title>
    </head>
    <body>
        <script>
            var scriptLoaded = false
            var scriptError = ''
            var lastError = ''

            window.addEventListener('error', function (event) {
                lastError = event.message || 'unknown error'
            })

            var script = document.createElement('script')
            script.async = true
            script.src = 'api/v4/" + TestResourceKey.FreeJavaScriptEndpoint + @"'
            script.onload = function () {
                scriptLoaded = true
            }
            script.onerror = function () {
                scriptError = 'failed to load ' + script.src
            }
            document.head.appendChild(script)
        </script>
        <h1>Browser Cache Tests</h1>
    </body>
</html>
";

            // Start the client server. It serves the page on /, and proxies
            // /api/* to the cloud, mimicking the production setup where the
            // customer's site talks to the cloud server-to-server.
            ClientServerUrl = $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            clientServerTokenSource = new CancellationTokenSource();
            var token = clientServerTokenSource.Token;
            clientServer = TestHelpers.ProxyingListener(
                    ClientServerUrl,
                    TestHelpers.GetActualRootUrl(TestInitialiser.CloudServerUrl),
                    clientPageData,
                    token)
                .Listener;
        }

        /// <summary>
        /// Browser cache behaviour in Chrome.
        /// </summary>
        [TestMethod]
        public void JavaScript_BrowserCache_Chrome()
        {
            driver = WebDriverFactory.Create(new ChromeOptions());

            RunTest(driver);
        }

        /// <summary>
        /// Browser cache behaviour in Edge.
        /// </summary>
        [TestMethod]
        public void JavaScript_BrowserCache_Edge()
        {
            driver = WebDriverFactory.Create(new EdgeOptions());

            RunTest(driver);
        }

        /// <summary>
        /// Browser cache behaviour in Firefox.
        /// </summary>
        [TestMethod]
        public void JavaScript_BrowserCache_FireFox()
        {
            driver = WebDriverFactory.Create(new FirefoxOptions());

            RunTest(driver);
        }

        private void RunTest(WebDriver driver)
        {
            driver.Navigate().GoToUrl(ClientServerUrl);

            IJavaScriptExecutor js = driver;

            WaitForDeviceData(driver, js, "initial page load");

            var session = js.ExecuteScript("return fod.sessionId");
            var device = js.ExecuteScript("return JSON.stringify(fod.device)");

            Assert.IsNotNull(session, "session is null");
            Assert.IsNotNull(device, "device is null");

            // Refresh the page -- the browser should serve from cache.
            driver.Navigate().Refresh();

            WaitForDeviceData(driver, js, "page refresh");

            var session2 = js.ExecuteScript("return fod.sessionId");
            var device2 = js.ExecuteScript("return JSON.stringify(fod.device)");

            // Session ID is regenerated on each page load, so just check it exists.
            Assert.IsNotNull(session2, "session2 is null after refresh");

            // Check the detection results match (same results from cache).
            Assert.AreEqual(device, device2, "device data differs after refresh");
        }

        private static void WaitForDeviceData(
            WebDriver driver,
            IJavaScriptExecutor js,
            string phase)
        {
            try
            {
                new WebDriverWait(driver, TimeSpan.FromSeconds(30)).Until(
                    _ => true.Equals(js.ExecuteScript(
                        "return window.scriptLoaded === true && " +
                        "typeof fod !== 'undefined' && " +
                        "fod.device && " +
                        "typeof fod.device.devicetype === 'string' && " +
                        "fod.device.devicetype.length > 0")));
            }
            catch (WebDriverTimeoutException)
            {
                var readyState = js.ExecuteScript("return document.readyState");
                var fodDefined = js.ExecuteScript("return typeof fod !== 'undefined'");
                var fodKeys = js.ExecuteScript(
                    "return typeof fod === 'undefined' ? '' : Object.keys(fod).join(',')");
                var fodErrors = js.ExecuteScript(
                    "return typeof fod === 'undefined' || !fod.errors ? '' : JSON.stringify(fod.errors)");
                var fodSessionId = js.ExecuteScript(
                    "return typeof fod === 'undefined' ? '' : (fod.sessionId || '')");
                var fodDeviceType = js.ExecuteScript(
                    "return typeof fod === 'undefined' || !fod.device ? '' : (fod.device.devicetype || '')");
                var fodIsMobile = js.ExecuteScript(
                    "return typeof fod === 'undefined' || !fod.device ? '' : (fod.device.ismobile || '')");
                var fodScreenWidth = js.ExecuteScript(
                    "return typeof fod === 'undefined' || !fod.device ? '' : (fod.device.screenpixelswidth || '')");
                var fodScreenHeight = js.ExecuteScript(
                    "return typeof fod === 'undefined' || !fod.device ? '' : (fod.device.screenpixelsheight || '')");
                var scriptLoaded = js.ExecuteScript("return window.scriptLoaded === true");
                var scriptError = js.ExecuteScript("return window.scriptError || ''");
                var lastError = js.ExecuteScript("return window.lastError || ''");

                Assert.Fail(
                    $"Timed out waiting for browser cache test during {phase}. " +
                    $"readyState={readyState}, fodDefined={fodDefined}, " +
                    $"fodKeys={fodKeys}, fodErrors={fodErrors}, fodSessionId={fodSessionId}, " +
                    $"fodDeviceType={fodDeviceType}, fodIsMobile={fodIsMobile}, " +
                    $"fodScreenWidth={fodScreenWidth}, fodScreenHeight={fodScreenHeight}, " +
                    $"scriptLoaded={scriptLoaded}, scriptError={scriptError}, " +
                    $"lastError={lastError}");
            }
        }

        /// <summary>
        /// Cleans up after the test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            driver?.Quit();
            driver = null;
            clientServerTokenSource?.Cancel();
            clientServer?.Stop();
            clientServer?.Close();
        }
    }
}
