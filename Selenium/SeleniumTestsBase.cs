using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using FiftyOne.Pipeline.Cloud.Tests.Common.TestElements;
using FiftyOne.Pipeline.JavaScriptBuilder;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Selenium
{
    /// <summary>
    /// Selenium Test base contains common methods for selenium tests. These
    /// tests check the various functions of the cloud generated JavaScript
    /// include using WebDrivers to simulate a browser environment.
    /// </summary>
    public abstract class SeleniumTestsBase
    {
        /// <summary>
        /// HTTP client for making requests.
        /// </summary>
        protected HttpClient httpClient;

        /// <summary>
        /// The client server URL.
        /// </summary>
        protected string ClientServerUrl;

        /// <summary>
        /// The web driver instance.
        /// </summary>
        protected WebDriver Driver;

        /// <summary>
        /// Per-test initialization. Subclasses should set Driver and
        /// ClientServerUrl from class-level shared resources, then call
        /// base.Init() to create the HttpClient.
        /// </summary>
        public virtual void Init()
        {
            httpClient = new HttpClient();
        }

        /// <summary>
        /// Stops an HttpListener without blocking sleeps.
        /// </summary>
        protected static void StopListener(HttpListener listener,
            CancellationTokenSource cts)
        {
            cts?.Cancel();
            listener?.Stop();
            listener?.Close();
        }

        /// <summary>
        /// Get the cloud generated JavaScript include and execute it using the 
        /// WebDriver. 
        /// </summary>
        /// <param name="endpoint">The JavaScript endpoint.</param>
        /// <param name="objectName">Custom object name for the FOD implemented by the JavaScript.</param>
        /// <param name="values">Additional properties to query the endpoint for.</param>
        /// <param name="license">Additional license to query the endpoint with.</param>
        /// <param name="parameters">Additional parameters to query the endpoint with.</param>
        /// <returns>IJavaScriptExecutor generated from the RemoteWebDriver</returns>
        internal IJavaScriptExecutor TestJavaScript(string endpoint, string objectName, string[] values, string license, params string[] parameters)
        {
            var allParameters = new List<string>();

            if(string.IsNullOrWhiteSpace(objectName) == false)
            {
                allParameters.Add($"{JavaScriptBuilder.Constants.EVIDENCE_OBJECT_NAME_SUFFIX}={objectName}");
            }
            else
            {
                objectName = JavaScriptBuilder.Constants.BUILDER_DEFAULT_OBJECT_NAME;
            }

            if (values.Length > 0)
            {
                allParameters.Add($"values={string.Join('+', values)}");
            }

            if (string.IsNullOrWhiteSpace(license) == false)
            {
                allParameters.Add($"license={license}");
            }

            if (parameters.Length > 0)
            {
                allParameters.AddRange(parameters);
            }

            var response = Get(endpoint, allParameters.ToArray());

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode,
                $"{endpoint} returned status code {response.StatusCode}.");

            var content = response.Content.ReadAsStringAsync().Result;
            Assert.IsFalse(String.IsNullOrEmpty(content),
                $"{endpoint} returned empty content.");

            IJavaScriptExecutor js = Driver;

            // Bind the object to window so the test can read it back later.
            js.ExecuteScript($"{content}; window.{objectName} = {objectName};") ;

            return js;
        }

        /// <summary>
        /// Query API test instance.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private HttpResponseMessage Get(string endpoint, params string[] parameters)
        {
            string queryString = string.Empty;
            if (parameters.Length > 0) 
            { 
                queryString = "?" + string.Join('&', parameters);
            }

            HttpRequestMessage message = new TestHttpRequestMessage(HttpMethod.Get, $"{TestInitialiser.CloudServerUrl}", $"api/v4/{endpoint}{queryString}");

            message.Headers.Add("User-Agent", UserAgent.CHROME_UA);

            return httpClient
                .SendAsync(message)
                .Result;
        }

        /// <summary>
        /// Provides test data for JavaScript_Get_ValidLicense tests.
        /// </summary>
        /// <returns>Test data for valid license scenarios.</returns>
        public static IEnumerable<object[]> JavaScript_Get_ValidLicense_DATA()
        {
            return new List<object[]>
            {
                new object[]{TestResourceKey.FreeJavaScriptEndpoint, "fod", new string[] { "device.ismobile", "device.devicetype" }, "" },
                new object[]{TestResourceKey.FreeJavaScriptEndpoint, "fod", new string[] { "device.hardwarevendor" }, "" },
                new object[]{TestResourceKey.PaidJavaScriptEndpoint, "myObject", new string[] { "device.devicetype", "device.priceband" }, TestValidateLicense.EnterpriseV4License},
                new object[]{TestResourceKey.PaidJavaScriptEndpoint, "myObject", new string[] { "device.frequencybands" }, TestValidateLicense.EnterpriseV4License},
                new object[]{TestResourceKey.FreeJavaScriptEndpoint, "fod", new string[] { "location.town" }, ""},
                new object[]{TestResourceKey.FreeJavaScriptEndpoint, "fod", new string[] { "location.town", "device.ismobile" }, ""},
                new object[]{TestResourceKey.FreeJavaScriptEndpoint, "fod", new string[] { "location.town", "device.ismobile" }, ""},
                new object[]{TestResourceKey.FreeJavaScriptEndpoint, "fod", new string[] { "location.country" }, "", "51D_Pos_latitude=51", "51D_Pos_longitude=0"},
                new object[]{TestResourceKey.PaidJavaScriptEndpoint, "fod", new string[] { "location.road" }, TestValidateLicense.EnterpriseV4License, "51D_Pos_latitude=51", "51D_Pos_longitude=0"},
            };
        }

        /// <summary>
        /// Provides test data for JavaScript_Get_NoLicense tests.
        /// </summary>
        /// <returns>Test data for no license scenarios.</returns>
        public static IEnumerable<object[]> JavaScript_Get_NoLicense_DATA()
        {
            return new List<object[]>
            {
                new object[]{TestResourceKey.FreeJavaScriptEndpoint, new string[] { "device.priceband" }, ""},
            };
        }

        /// <summary>
        /// Provides test data for JavaScript_Get_ClientSideOverrides tests.
        /// </summary>
        /// <returns>Test data for client-side overrides scenarios.</returns>
        public static IEnumerable<object[]> JavaScript_Get_ClientSideOverrides_DATA()
        {
            return new List<object[]>
            {
                new object[]{TestResourceKey.FreeJavaScriptEndpoint, "fod", new string[] { "device.screenpixelswidthjavascript", "device.screenpixelsheightjavascript", "device.screenpixelswidth", "device.screenpixelsheight" }, ""},
                new object[]{TestResourceKey.PaidJavaScriptEndpoint, "myObject", new string[] { "device.frequencybands" }, TestValidateLicense.EnterpriseV4License},
            };
        }

        /// <summary>
        /// Per-test cleanup. Disposes the HttpClient created in Init().
        /// Browser and server lifecycle is managed at the class level.
        /// </summary>
        [TestCleanup]
        public virtual void Cleanup()
        {
            httpClient?.Dispose();
        }
    }
}

