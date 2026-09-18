using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestElements;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.COEP
{
    /// <summary>
    /// Verify that the cloud server includes the Cross-Origin-Resource-Policy
    /// header, which is required for pages that set
    /// Cross-Origin-Embedder-Policy: require-corp.
    /// Without the CORP header the browser blocks cross-origin script loads.
    /// </summary>
    [TestClass, TestCategory("CloudInternal")]
    public class CoepTests
    {
        private string ClientServerUrl;
        private HttpListener clientServer;
        private CancellationTokenSource clientServerTokenSource;
        private WebDriver driver;

        /// <summary>
        /// Initializes the test.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            var cloudUrl = TestHelpers.GetActualRootUrl(
                TestInitialiser.CloudServerUrl);
            var resourceKey = TestResourceKey.FreeJavaScriptEndpoint;

            var pageData = @"
<!DOCTYPE html>
<html>
    <head>
        <title>COEP Test Page</title>
        <script async src=""" + cloudUrl + @"api/v4/" + resourceKey + @"""></script>
    </head>
    <body>
        <script>
            var test = 'loading'
            window.onload = function() {
                fod.complete(function (data) {
                    test = 'complete'
                })
            }
        </script>
        <h1>COEP Test</h1>
    </body>
</html>
";

            var port = TestHelpers.GetRandomUnusedPort();
            ClientServerUrl = $"http://localhost:{port}/";
            clientServerTokenSource = new CancellationTokenSource();
            var token = clientServerTokenSource.Token;

            clientServer = new HttpListener();
            clientServer.Prefixes.Add(ClientServerUrl);
            clientServer.Start();

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var ctx = await clientServer.GetContextAsync()
                            .ConfigureAwait(false);
                        if (token.IsCancellationRequested) break;

                        var response = ctx.Response;
                        response.Headers["Cross-Origin-Embedder-Policy"] =
                            "require-corp";
                        response.ContentType = "text/html";
                        response.StatusCode = 200;

                        var buffer = System.Text.Encoding.UTF8.GetBytes(pageData);
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(
                            buffer, 0, buffer.Length, token).ConfigureAwait(false);
                        response.Close();
                    }
                    catch (Exception) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            }, token);

            var options = new ChromeOptions();
            options.SetLoggingPreference(LogType.Browser, LogLevel.All);
            driver = WebDriverFactory.Create(options);
        }

        /// <summary>
        /// Navigate to a page that has Cross-Origin-Embedder-Policy: require-corp
        /// and verify that the cloud JavaScript resource loads successfully.
        /// This will only work if the cloud server sends the
        /// Cross-Origin-Resource-Policy: cross-origin header.
        /// </summary>
        [TestMethod]
        public void JavaScript_COEP_FodComplete()
        {
            driver.Navigate().GoToUrl(ClientServerUrl);

            IJavaScriptExecutor js = driver;

            try
            {
                new WebDriverWait(driver, TimeSpan.FromSeconds(10)).Until(
                    webDriver => js.ExecuteScript("return test").Equals("complete"));
            }
            catch (WebDriverTimeoutException)
            {
                var testVal = js.ExecuteScript("return test");
                var fodDefined = js.ExecuteScript(
                    "return typeof fod !== 'undefined'");
                var pageSource = driver.PageSource;
                var logs = driver.Manage().Logs
                    .GetLog(LogType.Browser)
                    .Select(e => $"[{e.Level}] {e.Message}")
                    .ToList();
                var consoleOutput = string.Join("\n", logs);

                Assert.Fail(
                    $"Timed out waiting for fod.complete.\n" +
                    $"test={testVal}\n" +
                    $"fod defined={fodDefined}\n" +
                    $"Console logs:\n{consoleOutput}\n" +
                    $"Page source:\n{pageSource}");
            }
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

