using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestElements;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using System;
using System.Net;
using System.Threading;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests
{
    /// <summary>
    /// Selenium tests for the cloud's third-party-cookie detection (the /api/v4/3pc
    /// endpoint and the 51Degrees JS include).
    ///
    /// The page is served from localhost while the cloud is hit on 127.0.0.1, so the
    /// browser treats the cloud's cookie as genuinely third-party.
    /// </summary>
    [TestClass, TestCategory("CloudInternal")]
    public class ThirdPartyCookieTests
    {
        private const string StatusElementId = "status";
        private const string ResultElementId = "result";
        private const string CheckingStatus = "Checking...";
        private const string CookieWasSentStatus = "COOKIE_WAS_SENT";
        private const string CookieWasNotSentStatus = "COOKIE_WAS_NOT_SENT";
        private const string ThirdPartyCookieEndpoint = "/api/v4/3pc";
        private const string CookieName = "51D_ThirdPartyCookiesEnabled";
        private const string FodObjectName = "fod";
        private const int DefaultTimeoutSeconds = 10;

        private IWebDriver _driver;

        /// <summary>
        /// URL of the client server that serves the test page (a stand-in for a
        /// customer's website).
        /// </summary>
        private string _clientServerUrl;

        /// <summary>
        /// URL of the cloud server hosting the /api/v4/3pc endpoint.
        /// </summary>
        private string _cloudServerUrl;

        private HttpListener _clientServer;
        private CancellationTokenSource _clientServerTokenSource;
        private WebDriverWait _wait;

        /// <summary>
        /// The cloud server URL with 127.0.0.1 in place of localhost, so the
        /// browser treats its cookies as truly third-party.
        /// </summary>
        private string CloudServerUrlCrossOrigin => _cloudServerUrl.Replace("localhost", "127.0.0.1");

        /// <summary>
        /// Waits for the cross-origin request to complete by checking that the
        /// status element no longer shows the initial "Checking..." text.
        /// </summary>
        private void WaitForRequestToComplete()
        {
            _wait.Until(d => d.FindElement(By.Id(StatusElementId)).Text != CheckingStatus);
        }

        /// <summary>
        /// Creates base Chrome options for headless testing.
        /// </summary>
        private ChromeOptions CreateBaseOptions()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            return options;
        }

        /// <summary>
        /// Creates the driver, using the remote Selenium grid when SELENIUM_URL is set
        /// and a local Chrome otherwise.
        /// </summary>
        private IWebDriver CreateDriver(ChromeOptions options)
        {
            if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
            {
                ExternalSeleniumHelper.AddExternalSeleniumArguments(options);
                return new RemoteWebDriver(new Uri(seleniumUrl), options);
            }
            return new ChromeDriver(options);
        }

        /// <summary>
        /// Stops the client server and cancels the associated token source.
        /// </summary>
        private void StopClientServer()
        {
            _clientServerTokenSource?.Cancel();
            if (_clientServer == null)
            {
                return;
            }

            // Bound the wait so teardown can't spin forever.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (_clientServer.IsListening && DateTime.UtcNow < deadline)
            {
                _clientServer.Stop();
                Thread.Sleep(100);
            }
            _clientServer.Close();
        }

        /// <summary>
        /// Stops the current client server and starts a new one serving the JS include test page.
        /// Uses 127.0.0.1 for the cloud server URL to ensure true cross-origin behavior.
        /// </summary>
        private void StartJsIncludeClientServer()
        {
            StopClientServer();

            _clientServerUrl = $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            _clientServerTokenSource = new CancellationTokenSource();
            var jsIncludeHtml = GenerateJsIncludeTestPageHtml(CloudServerUrlCrossOrigin);
            var serverListener = TestHelpers.SimpleListener(
                _clientServerUrl,
                jsIncludeHtml,
                _clientServerTokenSource.Token);
            _clientServer = serverListener.Listener;
        }

        /// <summary>
        /// Disposes the current driver and creates a new one with the specified options.
        /// </summary>
        private void RecreateDriver(ChromeOptions options)
        {
            _driver?.Quit();
            _driver?.Dispose();
            _driver = CreateDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(DefaultTimeoutSeconds));
        }

        /// <summary>
        /// Per-test setup: starts Chrome and the client HTTP server.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            var options = CreateBaseOptions();
            _driver = CreateDriver(options);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(DefaultTimeoutSeconds));

            _cloudServerUrl = TestInitialiser.CloudServerUrl;

            // The client server uses localhost while the cloud uses 127.0.0.1, so
            // requests to the cloud are cross-origin.
            _clientServerUrl = $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            _clientServerTokenSource = new CancellationTokenSource();

            var testPageHtml = GenerateTestPageHtml(CloudServerUrlCrossOrigin);

            var serverListener = TestHelpers.SimpleListener(
                _clientServerUrl,
                testPageHtml,
                _clientServerTokenSource.Token);
            _clientServer = serverListener.Listener;
        }

        /// <summary>
        /// Per-test cleanup: quits Chrome and stops the client HTTP server.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            _driver?.Quit();
            _driver?.Dispose();

            StopClientServer();
        }

        /// <summary>
        /// First cross-origin visit: no cookie yet, so /3pc returns status=false and sets it.
        /// </summary>
        [TestMethod]
        public void ThirdPartyCookie_FirstVisit_ReturnsSetStatus()
        {
            _driver.Navigate().GoToUrl(_clientServerUrl);
            WaitForRequestToComplete();

            var statusElement = _driver.FindElement(By.Id(StatusElementId));
            var statusText = statusElement.Text;

            Assert.IsTrue(statusText.Contains(CookieWasNotSentStatus),
                $"Expected status to contain '{CookieWasNotSentStatus}' on first visit (cookie not yet sent), but got: {statusText}");
        }

        /// <summary>
        /// Second visit: the cookie set on the first visit is sent back, so /3pc returns status=true.
        /// </summary>
        [TestMethod]
        public void ThirdPartyCookie_SecondVisit_ReturnsReceivedStatus()
        {
            _driver.Navigate().GoToUrl(_clientServerUrl);
            WaitForRequestToComplete();

            // Reload to send a second request; the cookie should come back with it.
            _driver.Navigate().Refresh();
            WaitForRequestToComplete();

            var statusElement = _driver.FindElement(By.Id(StatusElementId));
            var statusText = statusElement.Text;

            Assert.IsTrue(statusText.Contains(CookieWasSentStatus),
                $"Expected status to contain '{CookieWasSentStatus}' on second visit (cookie should be sent), but got: {statusText}. " +
                "This may indicate third-party cookies are blocked in the test browser.");
        }

        /// <summary>
        /// JS-include flow: the cloud sets the 3PC cookie on the .js response, the bundle's
        /// callback sends it back, and the resolved data carries thirdpartycookiesenabled="True".
        /// </summary>
        [TestMethod]
        public void ThirdPartyCookie_JsInclude_DetectsThirdPartyCookiesEnabled()
        {
            StartJsIncludeClientServer();

            _driver.Navigate().GoToUrl(_clientServerUrl);
            _wait.Until(d => d.FindElement(By.Id(StatusElementId)).Text == "complete");

            var resultElement = _driver.FindElement(By.Id(ResultElementId));
            var resultText = resultElement.Text;

            Assert.IsTrue(resultText.Contains("\"thirdpartycookiesenabled\":\"True\""),
                $"Expected thirdpartycookiesenabled to be 'True' in the data, " +
                $"indicating third-party cookies are enabled. Result: {resultText}");
        }

        /// <summary>
        /// When 3PC detection succeeds server-side, the thirdpartycookiesenabledjavascript
        /// snippet comes back empty (no client-side fallback needed).
        /// </summary>
        [TestMethod]
        public void ThirdPartyCookie_JsInclude_SuppressesJavaScriptSnippetWhenDetected()
        {
            StartJsIncludeClientServer();

            _driver.Navigate().GoToUrl(_clientServerUrl);
            _wait.Until(d => d.FindElement(By.Id(StatusElementId)).Text == "complete");

            var resultElement = _driver.FindElement(By.Id(ResultElementId));
            var resultText = resultElement.Text;

            Assert.IsTrue(
                resultText.Contains("\"thirdpartycookiesenabledjavascript\":\"\""),
                $"Expected thirdpartycookiesenabledjavascript to be empty (suppressed) when " +
                $"3PC detection succeeded server-side. Result: {resultText}");
        }

        /// <summary>
        /// With Chrome's --test-third-party-cookie-phaseout flag the cookie is blocked, so
        /// detection reports thirdpartycookiesenabled="False".
        /// </summary>
        [TestMethod]
        public void ThirdPartyCookie_JsInclude_PhaseoutFlagBlocksCookies()
        {
            var options = CreateBaseOptions();
            options.AddArgument("--test-third-party-cookie-phaseout");
            RecreateDriver(options);

            StartJsIncludeClientServer();

            _driver.Navigate().GoToUrl(_clientServerUrl);
            _wait.Until(d => d.FindElement(By.Id(StatusElementId)).Text == "complete");

            var resultText = _driver.FindElement(By.Id(ResultElementId)).Text;
            Assert.IsTrue(resultText.Contains("\"thirdpartycookiesenabled\":\"False\""),
                $"Expected thirdpartycookiesenabled to be 'False' with --test-third-party-cookie-phaseout flag. Result: {resultText}");
        }

        /// <summary>
        /// With third-party cookies blocked via Chrome profile prefs, the cloud cookie is never
        /// sent back, so detection reports thirdpartycookiesenabled="False".
        /// </summary>
        [TestMethod]
        public void ThirdPartyCookie_JsInclude_DetectsThirdPartyCookiesDisabled()
        {
            var options = CreateBaseOptions();
            options.AddUserProfilePreference("profile.cookie_controls_mode", 1);
            options.AddUserProfilePreference("profile.block_third_party_cookies", true);
            RecreateDriver(options);

            StartJsIncludeClientServer();

            _driver.Navigate().GoToUrl(_clientServerUrl);
            _wait.Until(d => d.FindElement(By.Id(StatusElementId)).Text == "complete");

            var resultText = _driver.FindElement(By.Id(ResultElementId)).Text;
            Assert.IsTrue(resultText.Contains("\"thirdpartycookiesenabled\":\"False\""),
                $"Expected thirdpartycookiesenabled to be 'False' when third-party cookies " +
                $"are blocked in the browser. Result: {resultText}");
        }

        /// <summary>
        /// Page served from origin A that fetches the cloud's /3pc endpoint cross-origin
        /// with credentials and shows the result.
        /// </summary>
        private string GenerateTestPageHtml(string cloudServerUrl)
        {
            var baseUrl = cloudServerUrl.TrimEnd('/');

            return $@"<!DOCTYPE html>
<html>
<body>
<div id=""{StatusElementId}"">{CheckingStatus}</div>
<div id=""{ResultElementId}""></div>
<script>
(async function() {{
    const s = document.getElementById('{StatusElementId}');
    const r = document.getElementById('{ResultElementId}');
    try {{
        const res = await fetch('{baseUrl}{ThirdPartyCookieEndpoint}', {{ credentials: 'include' }});
        const data = await res.json();
        s.textContent = data.status ? '{CookieWasSentStatus}' : '{CookieWasNotSentStatus}';
        r.textContent = 'Response: ' + JSON.stringify(data);
    }} catch (e) {{
        s.textContent = 'Error';
        r.textContent = e.message;
    }}
}})();
</script>
</body>
</html>";
        }

        /// <summary>
        /// Page served from origin A that loads the 51Degrees JS bundle from the cloud and
        /// reports the resolved data once fod.complete fires.
        /// </summary>
        private string GenerateJsIncludeTestPageHtml(string cloudServerUrl)
        {
            var baseUrl = cloudServerUrl.TrimEnd('/');
            var jsEndpoint = $"{baseUrl}/api/v4/{TestResourceKey.PaidJavaScriptEndpoint}";

            // Point the cookie-detection JavaScript at the test server rather than
            // the default cloud endpoint.
            var tpcEndpoint = $"{baseUrl}{ThirdPartyCookieEndpoint}";

            return $@"<!DOCTYPE html>
<html>
<body>
<div id=""{StatusElementId}"">{CheckingStatus}</div>
<div id=""{ResultElementId}""></div>
<script>window.fodTpcEndpoint = '{tpcEndpoint}';</script>
<script src=""{jsEndpoint}""></script>
<script>
{FodObjectName}.complete(function(data) {{
    document.getElementById('{ResultElementId}').textContent = JSON.stringify(data);
    document.getElementById('{StatusElementId}').textContent = 'complete';
}});
</script>
</body>
</html>";
        }
    }
}
