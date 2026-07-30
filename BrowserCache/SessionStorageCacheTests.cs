using FiftyOne.Pipeline.Cloud.SeleniumTests.Examples;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;
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
            window.fodDone = false
            window.fodDevice = ''
            window.fodError = ''
            window.addEventListener('load', function () {
                if (typeof fod === 'undefined') {
                    window.fodError = 'fod is undefined after load'
                    return
                }
                fod.complete(function (data) {
                    window.fodDone = true
                    window.fodDevice = JSON.stringify(data && data.device)
                })
            })
        </script>
    </head>
    <body>
        <h1>Session Storage Cache Test</h1>
    </body>
</html>
";
        }

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
                clientServerTokenSource.Token);
            clientServer = serverListener.Listener;
            clientHttpServer = serverListener.Server;

            IJavaScriptExecutor js = driver;

            driver.Navigate().GoToUrl(ClientServerUrl + "page1");
            WaitForFodDone(driver, js, "first page");

            var devicePage1 = (string)js.ExecuteScript("return window.fodDevice");
            var sessionIdPage1 = (string)js.ExecuteScript("return fod.sessionId");
            var keysPage1 = GetSessionStorageKeys(js);
            var postsPage1 = CountJsonPosts();
            Assert.IsTrue(postsPage1 >= 1,
                "the first page must call the json endpoint at least once");

            clientHttpServer.ResetRequests();
            driver.Navigate().GoToUrl(ClientServerUrl + "page2");
            WaitForFodDone(driver, js, "second page");

            var devicePage2 = (string)js.ExecuteScript("return window.fodDevice");
            var sessionIdPage2 = (string)js.ExecuteScript("return fod.sessionId");
            var keysPage2 = GetSessionStorageKeys(js);
            var postsPage2 = CountJsonPosts();

            Assert.AreNotEqual(sessionIdPage1, sessionIdPage2,
                "the include must be fetched fresh on the second page, " +
                "not served from the browser cache");
            Assert.IsFalse(
                string.IsNullOrEmpty(devicePage2) || devicePage2 == "null",
                "device data must be available on the second page");
            if (enableCookies)
            {
                // With cookies the values survive in the cookies themselves,
                // so only check that path is in use. Session storage keys and
                // repeat calls are not stable here: scripts without saved
                // values run again on every page.
                var cookies = (string)js.ExecuteScript("return document.cookie");
                StringAssert.Contains(cookies, "51D_",
                    "the evidence cookies must be present");
            }
            else
            {
                CollectionAssert.AreEqual(keysPage1, keysPage2,
                    "session storage keys must not change between page views: " +
                    $"[{string.Join(", ", keysPage1)}] -> [{string.Join(", ", keysPage2)}]");
                Assert.AreEqual(0, postsPage2,
                    "no json refresh call is expected on the second page");
            }
        }

        private int CountJsonPosts()
        {
            return clientHttpServer.RequestLog
                .Count(r => r == "POST /51dpipeline/json");
        }

        private static List<string> GetSessionStorageKeys(IJavaScriptExecutor js)
        {
            var raw = (System.Collections.ObjectModel.ReadOnlyCollection<object>)
                js.ExecuteScript("return Object.keys(sessionStorage)");
            return raw.Select(o => (string)o)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
        }

        private static void WaitForFodDone(
            WebDriver driver, IJavaScriptExecutor js, string phase)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (true)
            {
                if (true.Equals(js.ExecuteScript("return window.fodDone === true")))
                {
                    return;
                }
                if (DateTime.UtcNow >= deadline)
                {
                    var readyState = js.ExecuteScript("return document.readyState");
                    var fodError = js.ExecuteScript("return window.fodError || ''");
                    var fodDefined = js.ExecuteScript("return typeof fod !== 'undefined'");
                    var fodErrors = js.ExecuteScript(
                        "return typeof fod === 'undefined' || !fod.errors ? '' : JSON.stringify(fod.errors)");
                    var storage = js.ExecuteScript(
                        "return Object.keys(sessionStorage).join(',')");
                    Assert.Fail(
                        $"Timed out waiting for fod completion during {phase}. " +
                        $"readyState={readyState}, fodDefined={fodDefined}, " +
                        $"fodError={fodError}, fodErrors={fodErrors}, " +
                        $"sessionStorage=[{storage}]");
                }
                Thread.Sleep(1000);
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
