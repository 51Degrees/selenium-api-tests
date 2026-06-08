using System;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Selenium;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;
using System.Linq;
using System.Net;
using System.Threading;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browsers
{
    /// <summary>
    /// Performs Selenium Tests using a Gecko (Firefox) WebDriver.
    /// Browser and HTTP server are created once per class for performance.
    /// </summary>
    [TestClass, TestCategory("CloudInternal")]
    public class GeckoTests : SeleniumTestsBase
    {
        private static WebDriver s_driver;
        private static string s_clientServerUrl;
        private static HttpListener s_clientServer;
        private static CancellationTokenSource s_cts;

        /// <summary>
        /// Initializes the test class.
        /// </summary>
        /// <param name="context">The test context.</param>
        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            s_clientServerUrl = $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            s_cts = new CancellationTokenSource();
            s_clientServer = TestHelpers.SimpleListener(s_clientServerUrl, s_cts.Token);

            var options = new FirefoxOptions();
            options.AcceptInsecureCertificates = true;
            options.AddArgument("--headless");
            if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
            {
                ExternalSeleniumHelper.AddExternalSeleniumArguments(options);
                s_driver = new RemoteWebDriver(new Uri(seleniumUrl), options);
            }
            else
            {
                try
                {
                    s_driver = new FirefoxDriver(options);
                }
                catch (WebDriverException)
                {
                    Assert.Inconclusive("Could not create a gecko driver, check " +
                        "that the gecko driver is installed");
                }
            }
        }

        /// <summary>
        /// Initializes the test.
        /// </summary>
        [TestInitialize]
        public override void Init()
        {
            base.Init();
            Driver = s_driver;
            ClientServerUrl = s_clientServerUrl;
            Driver.Navigate().GoToUrl(ClientServerUrl);
        }

        /// <summary>
        /// Cleans up after the test class.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            s_driver?.Quit();
            s_driver = null;
            StopListener(s_clientServer, s_cts);
        }

        /// <summary>
        /// Test expected behavior when request contains valid license.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(JavaScript_Get_ValidLicense_DATA), typeof(SeleniumTestsBase))]
        public void JavaScript_Get_ValidLicense(string endpoint, string objectName, string[] values, string license, params string[] parameters)
        {
            var js = TestJavaScript(endpoint, objectName, values, license, parameters);

            foreach (var property in values)
            {
                var parts = property.Split('.');
                var element = string.Join('.', parts.Take(parts.Length - 1));
                var propertyName = parts.Last();

                if (element != null && propertyName != null)
                {
                    var value = js.ExecuteScript($"return {objectName}.{element}.{propertyName};");
                    Assert.IsNotNull(value, $"{objectName}.{element}.{propertyName} is null");
                }
                else
                {
                    Assert.Fail($"Could not split {property}");
                }
            }
        }

        /// <summary>
        /// Test expected behavior when request does not contain the required license.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(JavaScript_Get_NoLicense_DATA), typeof(SeleniumTestsBase))]
        public void JavaScript_Get_NoLicense(string endpoint, string[] values, string license, params string[] parameters)
        {
            var js = TestJavaScript(endpoint, "", values, license, parameters);

            foreach (var property in values)
            {
                var parts = property.Split('.');
                var element = string.Join('.', parts.Take(parts.Length - 1));
                var propertyName = parts.Last();

                if (element != null && propertyName != null)
                {
                    var propertyExists = js.ExecuteScript($"return fod.{element}.hasOwnProperty('{propertyName}');");
                    Assert.IsTrue((bool)propertyExists);

                    var value = js.ExecuteScript($"return fod.{element}.{propertyName};");
                    Assert.IsNotNull(value, $"fod.{element}.{propertyName} is null");

                    Assert.IsTrue(value.ToString().Contains($"{propertyName} is a paid feature.", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    Assert.Fail($"Could not split {property}");
                }
            }
        }

        /// <summary>
        /// Test expected behavior when request contains valid license.
        /// </summary>
        [TestMethod]
        [DynamicData(nameof(JavaScript_Get_ClientSideOverrides_DATA), typeof(SeleniumTestsBase))]
        public void JavaScript_Get_ClientSideOverrides(string endpoint, string objectName, string[] values, string license, params string[] parameters)
        {
            var js = TestJavaScript(endpoint, objectName, values, license, parameters);

            foreach (var property in values)
            {
                var parts = property.Split('.');
                var element = string.Join('.', parts.Take(parts.Length - 1));
                var propertyName = parts.Last();

                if (element != null && propertyName != null)
                {
                    var value = js.ExecuteScript($"return {objectName}.{element}.{propertyName};");
                    Assert.IsNotNull(value, $"{objectName}.{element}.{propertyName} is null");
                }
                else
                {
                    Assert.Fail($"Could not split {property}");
                }
            }
        }
    }
}

