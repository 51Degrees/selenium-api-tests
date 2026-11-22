using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.HumanConfidence;

/// <summary>
/// Checks <see cref="HumanConfidencePage"/> against a stand-in for the
/// client script served from this process, so that this repository's CI
/// shows the check passes on an active answer and fails on a passive one,
/// one with no source and one with a plain level, before any cloud or
/// example relies on it. Each test starts the browser before serving, so
/// the port picked for the page can't be the one the driver listens on.
/// </summary>
[TestClass, TestCategory("Browser")]
public class HumanConfidencePageTests
{
    private ScriptStandIn _script;
    private WebDriver _driver;

    /// <summary>Quits the browser and stops the stand-in.</summary>
    [TestCleanup]
    public void Cleanup()
    {
        _driver?.Quit();
        _driver = null;
        _script?.Dispose();
        _script = null;
    }

    /// <summary>The check passes on Active with a sealed value.</summary>
    [TestMethod]
    public void PassesWhereTheAnswerIsActiveAndSealed()
    {
        _driver = BrowserDrivers.CreateChrome(new ChromeOptions());
        _script = new ScriptStandIn(
            "{\"humanconfidencesource\":\"Active\"," +
            "\"humanconfidence\":\"stand-in-sealed-value\"}");
        var derived = HumanConfidencePage.AssertActiveAfterTheBrowserTests(
            _driver, _script.Page);
        Assert.AreEqual(
            "stand-in-sealed-value", (string)derived["humanconfidence"]);
    }

    /// <summary>The check fails where the answer stays Passive.</summary>
    [TestMethod]
    public void FailsWhereTheAnswerIsPassive()
    {
        _driver = BrowserDrivers.CreateChrome(new ChromeOptions());
        _script = new ScriptStandIn(
            "{\"humanconfidencesource\":\"Passive\"," +
            "\"humanconfidence\":\"Medium\"}");
        var failure = Assert.ThrowsExactly<AssertFailedException>(
            () => HumanConfidencePage.AssertActiveAfterTheBrowserTests(
                _driver, _script.Page));
        StringAssert.Contains(failure.Message, "\"Passive\"");
    }

    /// <summary>The check fails where the answer has no source.</summary>
    [TestMethod]
    public void FailsWhereTheAnswerHasNoSource()
    {
        _driver = BrowserDrivers.CreateChrome(new ChromeOptions());
        _script = new ScriptStandIn(
            "{\"humanconfidence\":\"stand-in-sealed-value\"}");
        var failure = Assert.ThrowsExactly<AssertFailedException>(
            () => HumanConfidencePage.AssertActiveAfterTheBrowserTests(
                _driver, _script.Page));
        StringAssert.Contains(failure.Message, "the answer has none");
    }

    /// <summary>
    /// The check fails where the source is Active but the level is plain.
    /// </summary>
    [TestMethod]
    public void FailsWhereTheLevelIsPlain()
    {
        _driver = BrowserDrivers.CreateChrome(new ChromeOptions());
        _script = new ScriptStandIn(
            "{\"humanconfidencesource\":\"Active\"," +
            "\"humanconfidence\":\"High\"}");
        var failure = Assert.ThrowsExactly<AssertFailedException>(
            () => HumanConfidencePage.AssertActiveAfterTheBrowserTests(
                _driver, _script.Page));
        StringAssert.Contains(failure.Message, "the plain level High");
    }

    /// <summary>
    /// Serves a page and a stand-in client script, whose fod.complete hands
    /// every function the derived object it was given, from a port on this
    /// machine.
    /// </summary>
    private sealed class ScriptStandIn : IDisposable
    {
        private const string PageHtml =
            "<!DOCTYPE html><html><head><title>Script stand-in</title>"
            + "<script src='/51D.js'></script></head><body>"
            + "<p>Script stand-in</p></body></html>";

        private readonly string _script;
        private readonly HttpListener _listener = new HttpListener();
        private readonly Task _serving;

        /// <summary>Starts serving on a free port.</summary>
        public ScriptStandIn(string derived)
        {
            _script = "window.fod = { complete: function (callback) {"
                + " setTimeout(function () { callback({ derived: "
                + derived + " }); }, 10); } };";
            Page = new Uri(
                $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/");
            _listener.Prefixes.Add(Page.ToString());
            _listener.Start();
            _serving = Task.Run(Serve);
        }

        /// <summary>The stand-in page.</summary>
        public Uri Page { get; }

        /// <summary>Stops serving.</summary>
        public void Dispose()
        {
            _listener.Close();
            try
            {
                _serving.Wait();
            }
            catch (AggregateException)
            {
                // The listener was closed under a pending request.
            }
        }

        private async Task Serve()
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
                var response = context.Response;
                var script = context.Request.Url.AbsolutePath == "/51D.js";
                response.ContentType = script
                    ? "application/javascript"
                    : "text/html";
                var bytes = Encoding.UTF8.GetBytes(script ? _script : PageHtml);
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.Close();
            }
        }
    }
}
