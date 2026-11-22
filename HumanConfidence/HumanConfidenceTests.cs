using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Examples;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.HumanConfidence;

/// <summary>
/// Human Confidence against a cloud service, with the resource key in
/// _51DEGREES_RESOURCE_KEY_BESPOKE and the service in
/// _51DEGREES_CLOUD_ENDPOINT. A request with no browser tests answers
/// HumanConfidenceSource Passive. A page that runs the client script
/// answers Active once the browser tests have run, with HumanConfidence
/// sealed. Each request names both properties, so the answer carries them
/// whatever the key's own property list holds. The key is never printed.
/// </summary>
[TestClass, TestCategory("HumanConfidence")]
public class HumanConfidenceTests
{
    private const int RequestSeconds = 20;

    private const string ChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";

    private WebDriver _driver;
    private HttpListener _listener;
    private Task _serving;

    /// <summary>Quits the browser and stops serving the page.</summary>
    [TestCleanup]
    public void Cleanup()
    {
        _driver?.Quit();
        _driver = null;
        _listener?.Close();
        try
        {
            _serving?.Wait();
        }
        catch (AggregateException)
        {
            // The listener was closed under a pending request.
        }
        _listener = null;
    }

    /// <summary>
    /// The key's JSON answer, asked with no browser tests, has the source
    /// Passive and a plain level.
    /// </summary>
    [TestMethod]
    public async Task Json_SourceIsPassiveWithNoBrowserTests()
    {
        var key = Key();
        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(RequestSeconds),
        };
        var form = new Dictionary<string, string>
        {
            ["user-agent"] = ChromeUserAgent,
            ["values"] = HumanConfidencePage.Values,
        };
        using var answer = await http.PostAsync(
            Api() + key + ".json", new FormUrlEncodedContent(form));
        var body = (await answer.Content.ReadAsStringAsync())
            .Replace(key, "<key>", StringComparison.Ordinal);
        Assert.AreEqual(HttpStatusCode.OK, answer.StatusCode, body);

        var derived = JObject.Parse(body)["derived"] as JObject;
        Assert.IsNotNull(derived, "The answer has no derived object.");
        Assert.AreEqual(
            "Passive",
            (string)derived["humanconfidencesource"],
            "A request with no browser tests should be Passive, but the " +
            "derived object is " + derived.ToString(
                Newtonsoft.Json.Formatting.None));
        CollectionAssert.Contains(
            (System.Collections.ICollection)HumanConfidencePage.Levels,
            (string)derived["humanconfidence"],
            "A passive level is one of the three plain levels.");
    }

    /// <summary>
    /// A page that runs the key's client script in Chrome gets the source
    /// Active and a sealed HumanConfidence once the browser tests have run.
    /// </summary>
    [TestMethod]
    public void Chrome_SourceIsActiveAfterTheBrowserTests()
    {
        var page = ServeScriptPage(Key());
        _driver = BrowserDrivers.CreateChrome(new ChromeOptions());
        HumanConfidencePage.AssertActiveAfterTheBrowserTests(_driver, page);
        Console.WriteLine(
            "  Chrome: Active, with a sealed HumanConfidence.");
    }

    /// <summary>
    /// The resource key, or a failure naming the variable.
    /// </summary>
    private static string Key()
    {
        var key = TestConfig.Instance().Optional(
            TestConfig.PaidResourceKeyVariable);
        if (key == null)
        {
            Assert.Fail(
                $"{TestConfig.PaidResourceKeyVariable} is not set. Set it " +
                "to a resource key that carries HumanConfidence and " +
                "HumanConfidenceSource.");
        }
        return key;
    }

    /// <summary>
    /// The service's API base, ending api/v4/, whether or not
    /// _51DEGREES_CLOUD_ENDPOINT includes the path.
    /// </summary>
    private static string Api() => TestConfig.Instance().RootUrl + "api/v4/";

    /// <summary>
    /// Serves, from a port on this machine, a page that loads the key's
    /// client script naming both properties.
    /// </summary>
    private Uri ServeScriptPage(string key)
    {
        var page = new Uri(
            $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/");
        var html = Encoding.UTF8.GetBytes(
            "<!DOCTYPE html><html><head><title>Human Confidence</title>" +
            $"<script src='{Api()}{key}.js?values=" +
            $"{HumanConfidencePage.Values}'></script>" +
            "</head><body><p>Human Confidence</p></body></html>");
        _listener = new HttpListener();
        _listener.Prefixes.Add(page.ToString());
        _listener.Start();
        _serving = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync()
                        .ConfigureAwait(false);
                }
                catch (Exception e) when (
                    e is HttpListenerException
                    || e is ObjectDisposedException)
                {
                    return;
                }
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = html.Length;
                context.Response.OutputStream.Write(html, 0, html.Length);
                context.Response.Close();
            }
        });
        return page;
    }
}

/// <summary>
/// A running Human Confidence example's page, named by
/// _51DEGREES_EXAMPLE_URL, checked in Chrome. The page loads the client
/// script for the example's own key, so the answer carries the source when
/// that key's property list brings it.
/// </summary>
[TestClass, TestCategory("HumanConfidenceExample")]
public class HumanConfidenceExampleTests
{
    private WebDriver _driver;

    /// <summary>Quits the browser.</summary>
    [TestCleanup]
    public void Cleanup()
    {
        _driver?.Quit();
        _driver = null;
    }

    /// <summary>
    /// The example's page gets the source Active and a sealed
    /// HumanConfidence once the browser tests have run.
    /// </summary>
    [TestMethod]
    public void Chrome_ExamplePageSourceIsActiveAfterTheBrowserTests()
    {
        var value = Environment.GetEnvironmentVariable(
            ExampleApps.ExampleUrlVar);
        if (string.IsNullOrEmpty(value))
        {
            Assert.Fail(
                $"{ExampleApps.ExampleUrlVar} is not set. Set it to the " +
                "address of a running Human Confidence example.");
        }
        _driver = BrowserDrivers.CreateChrome(new ChromeOptions());
        HumanConfidencePage.AssertActiveAfterTheBrowserTests(
            _driver, new Uri(value));
    }
}
