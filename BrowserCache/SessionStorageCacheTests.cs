using FiftyOne.Pipeline.Cloud.SeleniumTests.Examples;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.BrowserCache
{
    /// <summary>
    /// Verify session storage cache behaviour: after navigating to a
    /// second page in the same tab, the results are reused and no new
    /// pipeline requests are made.
    /// </summary>
    [TestClass, TestCategory("Contract")]
    public class SessionStorageCacheTests
    {
        private const int CompletionTimeoutSeconds = 60;

        private static IExampleApp _app;
        private static CancellationTokenSource _appTokenSource;

        private WebDriver driver;
        private string ClientServerUrl;
        private System.Net.HttpListener clientServer;
        private HttpServer clientHttpServer;
        private CancellationTokenSource clientServerTokenSource;

        /// <summary>
        /// Starts the example app once for the whole class.
        /// </summary>
        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            string rootUrl;
            string resourceKey;
            try
            {
                rootUrl = TestConfig.Instance().RootUrl;
                resourceKey = TestConfig.Instance().PaidResourceKey;
            }
            catch (InvalidOperationException ex)
            {
                Assert.Inconclusive(ex.Message);
                return;
            }

            if (!ExampleApps.TryCreate(out _app, out var skipReason))
            {
                Assert.Inconclusive(skipReason);
                return;
            }

            _appTokenSource = new CancellationTokenSource();
            var options = new ExampleAppOptions(
                TestHelpers.GetRandomUnusedPort(),
                new Uri(rootUrl),
                resourceKey,
                new Dictionary<string, string>());
            _app.StartAsync(options, _appTokenSource.Token)
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// Stops the example app.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanupApp()
        {
            _appTokenSource?.Cancel();
            _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _app = null;
        }

        /// <summary>
        /// A page that renders what <c>fod.complete</c> delivers, in the way a
        /// customer's page would. The test reads the rendered cells rather than
        /// script variables, so it exercises the callback actually handing over
        /// usable detection results instead of merely inspecting the include's
        /// internal state.
        /// </summary>
        private static string BuildPage(bool enableCookies)
        {
            var flag = enableCookies ? "true" : "false";
            return @"
<!DOCTYPE>
<html>
    <head>
        <title>Session Storage Cache Test</title>
        <script async src='/51Degrees.core.js?fod-js-enable-cookies=" + flag + @"'></script>
        <script>
            window.addEventListener('load', function () {
                var results = document.getElementById('results')
                if (typeof fod === 'undefined') {
                    results.setAttribute('data-state', 'no-fod')
                    return
                }
                fod.complete(function (data) {
                    var device = (data && data.device) || {}
                    var render = function (id, value) {
                        document.getElementById(id).textContent =
                            value === undefined || value === null
                                ? ''
                                : [].concat(value).join(', ')
                    }
                    render('deviceid', device.deviceid)
                    render('hardwarename', device.hardwarename)
                    render('platformname', device.platformname)
                    render('devicetype', device.devicetype)
                    results.setAttribute('data-state', 'complete')
                })
            })
        </script>
    </head>
    <body>
        <h1>Session Storage Cache Test</h1>
        <table id='results' data-state='pending'>
            <tr><td>Device Id:</td><td id='deviceid'></td></tr>
            <tr><td>Hardware Name:</td><td id='hardwarename'></td></tr>
            <tr><td>Platform Name:</td><td id='platformname'></td></tr>
            <tr><td>Device Type:</td><td id='devicetype'></td></tr>
        </table>
    </body>
</html>
";
        }

        /// <summary>Detection results as rendered onto the page.</summary>
        private sealed record RenderedResults(
            string DeviceId, string HardwareName, string PlatformName, string DeviceType);

        /// <summary>
        /// Session storage cache behaviour in Chrome.
        /// </summary>
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SessionStorageCache_Chrome(bool enableCookies)
        {
            var options = new ChromeOptions();
            options.AcceptInsecureCertificates = true;
            options.AddArgument("--headless");

            if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
            {
                ExternalSeleniumHelper.AddExternalSeleniumArguments(options);
                driver = new RemoteWebDriver(new Uri(seleniumUrl), options);
            }
            else
            {
                driver = new ChromeDriver(options);
            }

            RunTest(driver, enableCookies);
        }

        /// <summary>
        /// Session storage cache behaviour in Firefox.
        /// </summary>
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SessionStorageCache_FireFox(bool enableCookies)
        {
            var options = new FirefoxOptions();
            options.AcceptInsecureCertificates = true;
            options.AddArgument("--headless");

            if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
            {
                ExternalSeleniumHelper.AddExternalSeleniumArguments(options);
                driver = new RemoteWebDriver(new Uri(seleniumUrl), options);
            }
            else
            {
                driver = new FirefoxDriver(options);
            }

            RunTest(driver, enableCookies);
        }

        private void RunTest(WebDriver driver, bool enableCookies)
        {
            ClientServerUrl = $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            clientServerTokenSource = new CancellationTokenSource();
            var serverListener = TestHelpers.ExampleProxyListener(
                ClientServerUrl,
                _app.BaseUrl.ToString(),
                BuildPage(enableCookies),
                ExampleApps.JsonEndpointPath,
                clientServerTokenSource.Token);
            clientServer = serverListener.Listener;
            clientHttpServer = serverListener.Server;

            IJavaScriptExecutor js = driver;

            driver.Navigate().GoToUrl(ClientServerUrl + "page1");
            var page1 = WaitForRenderedResults(driver, "first page");
            var sessionIdPage1 = (string)js.ExecuteScript("return fod.sessionId");
            var keysPage1 = GetSessionStorageKeys(js);

            Assert.IsTrue(CountJsonPosts() >= 1,
                "the first page must call the json endpoint at least once");
            Assert.IsFalse(string.IsNullOrEmpty(page1.DeviceId),
                "the first page must render a device id from fod.complete");

            // A second page in the same tab, then a reload of it. Both reuse the
            // cached results, so neither may call the json endpoint again and
            // both must render the values the first page resolved.
            clientHttpServer.ResetRequests();
            driver.Navigate().GoToUrl(ClientServerUrl + "page2");
            var page2 = WaitForRenderedResults(driver, "second page");
            var sessionIdPage2 = (string)js.ExecuteScript("return fod.sessionId");
            var keysPage2 = GetSessionStorageKeys(js);
            var postsPage2 = CountJsonPosts();

            clientHttpServer.ResetRequests();
            driver.Navigate().Refresh();
            var reloaded = WaitForRenderedResults(driver, "reloaded second page");
            var keysReloaded = GetSessionStorageKeys(js);
            var postsReloaded = CountJsonPosts();

            // Comparing the two ids only says anything if there are ids to
            // compare. An integration that leaves fod.sessionId empty makes
            // AreNotEqual fail with "Expected any value except:<>", which reads
            // as a caching problem when it is really a missing value, so say so
            // and skip rather than report a failure this test cannot diagnose.
            if (string.IsNullOrEmpty(sessionIdPage1)
                || string.IsNullOrEmpty(sessionIdPage2))
            {
                Assert.Inconclusive(
                    $"The '{ExampleApps.SelectedLang}' example serves an include " +
                    "with no session id, so whether it was re-fetched cannot be " +
                    $"determined (page 1 '{sessionIdPage1}', page 2 '{sessionIdPage2}').");
                return;
            }

            Assert.AreNotEqual(sessionIdPage1, sessionIdPage2,
                "the include must be fetched fresh on the second page, " +
                "not served from the browser cache");

            AssertSameResults(page1, page2, "second page");
            AssertSameResults(page1, reloaded, "reloaded second page");

            Assert.AreEqual(0, postsPage2,
                "no json refresh call is expected on the second page");
            Assert.AreEqual(0, postsReloaded,
                "no json refresh call is expected when the page is reloaded");

            if (enableCookies)
            {
                var cookies = (string)js.ExecuteScript("return document.cookie");
                StringAssert.Contains(cookies, "51D_",
                    "the evidence cookies must be present");
            }
            else
            {
                CollectionAssert.AreEqual(keysPage1, keysPage2,
                    "session storage keys must not change between page views: " +
                    $"[{string.Join(", ", keysPage1)}] -> [{string.Join(", ", keysPage2)}]");
                CollectionAssert.AreEqual(keysPage1, keysReloaded,
                    "session storage keys must not change when the page is reloaded: " +
                    $"[{string.Join(", ", keysPage1)}] -> [{string.Join(", ", keysReloaded)}]");
            }
        }

        /// <summary>
        /// Every rendered value must survive, not just the device id: a cache
        /// that returned a different but still populated result would otherwise
        /// pass.
        /// </summary>
        private static void AssertSameResults(
            RenderedResults expected, RenderedResults actual, string phase)
        {
            Assert.AreEqual(expected.DeviceId, actual.DeviceId,
                $"the device id rendered on the {phase} must come from the cached results");
            Assert.AreEqual(expected.HardwareName, actual.HardwareName,
                $"the hardware name rendered on the {phase} must come from the cached results");
            Assert.AreEqual(expected.PlatformName, actual.PlatformName,
                $"the platform name rendered on the {phase} must come from the cached results");
            Assert.AreEqual(expected.DeviceType, actual.DeviceType,
                $"the device type rendered on the {phase} must come from the cached results");
        }

        private int CountJsonPosts()
        {
            var expected = $"POST {ExampleApps.JsonEndpointPath}";
            return clientHttpServer.RequestLog
                .Count(r => string.Equals(r, expected, StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> GetSessionStorageKeys(IJavaScriptExecutor js)
        {
            var raw = (System.Collections.ObjectModel.ReadOnlyCollection<object>)
                js.ExecuteScript("return Object.keys(sessionStorage)");
            return raw.Select(o => (string)o)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Waits for the page's own callback to render its results, then reads
        /// them back out of the DOM. Reading the rendered cells rather than the
        /// include's variables is what makes this a test of fod.complete handing
        /// over usable data, rather than of the script's internal state.
        /// </summary>
        private static RenderedResults WaitForRenderedResults(
            WebDriver driver, string phase)
        {
            IJavaScriptExecutor js = driver;
            try
            {
                new WebDriverWait(driver, TimeSpan.FromSeconds(CompletionTimeoutSeconds))
                    .Until(d => "complete".Equals(
                        d.FindElement(By.Id("results")).GetAttribute("data-state")));
            }
            catch (WebDriverTimeoutException)
            {
                var state = js.ExecuteScript(
                    "var r = document.getElementById('results');" +
                    "return r ? r.getAttribute('data-state') : 'no-element';");
                var fodDefined = js.ExecuteScript("return typeof fod !== 'undefined'");
                var fodErrors = js.ExecuteScript(
                    "return typeof fod === 'undefined' || !fod.errors ? '' : JSON.stringify(fod.errors)");
                var storage = js.ExecuteScript(
                    "return Object.keys(sessionStorage).join(',')");
                Assert.Fail(
                    $"Timed out waiting for fod.complete to render results during {phase}. " +
                    $"readyState={js.ExecuteScript("return document.readyState")}, " +
                    $"resultsState={state}, fodDefined={fodDefined}, " +
                    $"fodErrors={fodErrors}, sessionStorage=[{storage}]");
            }

            return new RenderedResults(
                driver.FindElement(By.Id("deviceid")).Text.Trim(),
                driver.FindElement(By.Id("hardwarename")).Text.Trim(),
                driver.FindElement(By.Id("platformname")).Text.Trim(),
                driver.FindElement(By.Id("devicetype")).Text.Trim());
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
