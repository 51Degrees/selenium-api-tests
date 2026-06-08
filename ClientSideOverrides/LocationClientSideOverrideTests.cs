using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestElements;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.ClientSideOverrides
{
    /// <summary>
    /// Test functionality of Client-Side Overrides for the Location feature.
    /// </summary>
    [TestClass, TestCategory("CloudInternal")]
    public class LocationClientSideOverrideTests
    {
        private string ClientServerUrl;
        private HttpListener clientServer;
        private CancellationTokenSource clientServerTokenSource;
        private ChromeDriver driver;
        private const int JavaScriptTimeout = 60; // in seconds

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
        <script async src=""api/v4/" + TestResourceKey.PaidJavaScriptEndpoint + @"""></script>
    </head>
    <body>
        <script>
            var doc = 'loading'
            var test = 'loading'
            var country = 'loading'
            var road = 'loading'

            window.onload = function() {
                doc = 'complete'
            }

            buttonClicked = function() {
                fod.complete(function (data) {
                    country = data.location['country']
                    road = data.location['road']
                    test = 'complete'
                }, 'location')
            }
        </script>
        <button id=""locationbutton"" type=""button"" onclick=""buttonClicked()"">Use my location</button>
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
        /// Test that the location JavaScript gets a location, location gets
        /// sent to the cloud and a geo-location response returned.
        /// </summary>
        [TestMethod]
        [Ignore("--headless doesn't work. Ignoring test until we can investigate and resolve the issue.")]
        public void JavaScript_ClientSideOverrides()
        {
            var chromeOptions = new ChromeOptions();
            // allow insecure connections
            chromeOptions.AcceptInsecureCertificates = true;
            // run in headless mode.
            chromeOptions.AddArgument("--headless");

            chromeOptions.SetLoggingPreference(LogType.Browser, LogLevel.All);

            driver = new ChromeDriver(chromeOptions);

            // allow geo-location on test client website
            driver.ExecuteCdpCommand("Browser.grantPermissions",
                new Dictionary<string, object>()
                {
                    { "origin", ClientServerUrl },
                    { "permissions", new string[]{ "geolocation" } }
                });

            // set the browser location
            driver.ExecuteCdpCommand("Emulation.setGeolocationOverride", 
                new Dictionary<string, object>() 
                {
                    { "latitude", 51 },
                    { "longitude", -1 },
                    { "accuracy", 100 },
                });

            // navigate to the test client website and use the location feature.
            driver.Navigate().GoToUrl(ClientServerUrl);

            IJavaScriptExecutor js = driver;

            // Wait for the windows to finish loading.
            new WebDriverWait(driver, TimeSpan.FromSeconds(5)).Until(
                webDriver => js.ExecuteScript("return doc").Equals("complete"));

            // Click the 'use my location' button
            driver.FindElement(By.Id("locationbutton")).Click();

            // Wait until the values have been updated.
            new WebDriverWait(driver, TimeSpan.FromSeconds(JavaScriptTimeout)).Until(
                webDriver =>
                    js.ExecuteScript("return test").Equals("complete") &&
                    js.ExecuteScript("return country").Equals("loading") == false &&
                    js.ExecuteScript("return road").Equals("loading") == false);

            var country = js.ExecuteScript($"return country");
            var road = js.ExecuteScript($"return road");

            // Check the values are returned, evidence will have been passed
            // even if the lcoation does not match. The default response is 
            // configured in the nominatimSample in Cloud.Tests.Common
            Assert.AreEqual("United Kingdom", country, "country does not match");
            Assert.AreEqual("Greyfriars Road", road, "road does not match");
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
