using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.ModuleJavaScript;

/// <summary>
/// Checks <see cref="ModuleJavaScriptPage"/> against a stand-in for a
/// module's JavaScript example served from this process, so that this
/// repository's CI shows the check passes on a working page and fails on a
/// broken one before any module's CI relies on it.
/// </summary>
[TestClass, TestCategory("Browser")]
public class ModuleJavaScriptPageTests
{
    private ModuleStandIn _module;
    private WebDriver _driver;

    /// <summary>Quits the browser and stops the stand-in.</summary>
    [TestCleanup]
    public void Cleanup()
    {
        _driver?.Quit();
        _driver = null;
        _module?.Dispose();
        _module = null;
    }

    /// <summary>The check passes in Chrome on a working page.</summary>
    [TestMethod]
    public void Chrome_PassesWhereTheModuleRanItsJavaScript()
    {
        _module = new ModuleStandIn(ModuleStandIn.Behaviour.Working);
        _driver = ModuleJavaScriptPage.StartChrome();
        ModuleJavaScriptPage.AssertRanTheModulesJavaScript(
            _driver, _module.Page);
    }

    /// <summary>The check passes in Firefox on a working page.</summary>
    [TestMethod]
    public void Firefox_PassesWhereTheModuleRanItsJavaScript()
    {
        _module = new ModuleStandIn(ModuleStandIn.Behaviour.Working);
        _driver = BrowserDrivers.CreateFirefox(new FirefoxOptions());
        ModuleJavaScriptPage.AssertRanTheModulesJavaScript(
            _driver, _module.Page);
    }

    /// <summary>
    /// The check fails, passing on the module's note, where the module
    /// serves no JavaScript, as it does with a data file that holds none.
    /// The page still shows a width there, being the one the data file
    /// holds for the User-Agent.
    /// </summary>
    [TestMethod]
    public void FailsWhereTheModuleServedNoJavaScript()
    {
        _module = new ModuleStandIn(ModuleStandIn.Behaviour.NoJavaScript);
        _driver = ModuleJavaScriptPage.StartChrome();
        var failure = Assert.ThrowsExactly<AssertFailedException>(
            () => ModuleJavaScriptPage.AssertRanTheModulesJavaScript(
                _driver, _module.Page));
        StringAssert.Contains(
            failure.Message, ModuleJavaScriptPage.WidthCookie);
        StringAssert.Contains(failure.Message, ModuleStandIn.Note);
    }

    /// <summary>
    /// The check fails where the JavaScript ran but the module answered
    /// without the evidence it gave.
    /// </summary>
    [TestMethod]
    public void FailsWhereTheModuleIgnoredTheEvidence()
    {
        _module = new ModuleStandIn(ModuleStandIn.Behaviour.IgnoresEvidence);
        _driver = ModuleJavaScriptPage.StartChrome();
        var failure = Assert.ThrowsExactly<AssertFailedException>(
            () => ModuleJavaScriptPage.AssertRanTheModulesJavaScript(
                _driver, _module.Page));
        StringAssert.Contains(
            failure.Message, $"shows the width '{ModuleStandIn.NoWidth}'");
    }

    /// <summary>
    /// Serves a page, the JavaScript a module serves and the module's
    /// answer, in the shape of the NGINX and Varnish examples, from a port
    /// on this machine.
    /// </summary>
    private sealed class ModuleStandIn : IDisposable
    {
        /// <summary>What the stand-in module does.</summary>
        public enum Behaviour
        {
            /// <summary>
            /// Serves the JavaScript and answers with the width it gave.
            /// </summary>
            Working,

            /// <summary>
            /// Serves no JavaScript and adds a note, as a module does with
            /// a data file that holds none for the property.
            /// </summary>
            NoJavaScript,

            /// <summary>
            /// Serves the JavaScript and answers without the width it gave.
            /// </summary>
            IgnoresEvidence,
        }

        /// <summary>The width the module answers without evidence.</summary>
        public const string NoWidth = "Unknown";

        /// <summary>The note the module adds without JavaScript.</summary>
        public const string Note = "ScreenPixelsWidthJavascript needs a "
            + "data file that holds it.";

        // Loads the module's JavaScript, sends the 51D_ cookies to the
        // module as the examples do, and writes its answer into the page.
        private const string PageHtml =
            "<!DOCTYPE html><html><head><title>Module stand-in</title>"
            + "<script src='/51D.js'></script></head><body>"
            + "<p id='screenpixelswidth'></p><p id='enterprise'></p><script>"
            + "var evidence = document.cookie.split(/; */).filter("
            + "function (c) { return c.indexOf('51D_') === 0; });"
            + "var xhr = new XMLHttpRequest();"
            + "xhr.open('GET', '/overrides?' + evidence.join('&'));"
            + "xhr.onload = function () {"
            + "var note = xhr.getResponseHeader('x-enterprise');"
            + "if (note) { document.getElementById('enterprise')"
            + ".textContent = note; }"
            + "document.getElementById('screenpixelswidth').innerHTML ="
            + " 'Screen Pixels Width: <span id=widthvalue>' +"
            + " xhr.getResponseHeader('x-screenpixelswidth') + '</span>';"
            + "};"
            + "xhr.send();"
            + "</script></body></html>";

        private const string JavaScript =
            "document.cookie = \"51D_ScreenPixelsWidth=\" + screen.width;";

        private const string NoJavaScriptComment =
            "/* 51Degrees Javascript not available. */";

        private readonly Behaviour _behaviour;
        private readonly HttpListener _listener = new HttpListener();
        private readonly Task _serving;

        /// <summary>Starts serving on a free port.</summary>
        public ModuleStandIn(Behaviour behaviour)
        {
            _behaviour = behaviour;
            Page = new Uri(
                $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/");
            _listener.Prefixes.Add(Page.ToString());
            _listener.Start();
            _serving = Task.Run(Serve);
        }

        /// <summary>The stand-in example's page.</summary>
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
                Answer(context);
            }
        }

        private void Answer(HttpListenerContext context)
        {
            var response = context.Response;
            string body;
            switch (context.Request.Url.AbsolutePath)
            {
                case "/51D.js":
                    response.ContentType = "application/x-javascript";
                    body = _behaviour == Behaviour.NoJavaScript
                        ? NoJavaScriptComment
                        : JavaScript;
                    break;
                case "/overrides":
                    var evidence = context.Request.QueryString[
                        ModuleJavaScriptPage.WidthCookie];
                    response.AddHeader(
                        "x-screenpixelswidth",
                        _behaviour == Behaviour.Working && evidence != null
                            ? evidence
                            : NoWidth);
                    if (_behaviour == Behaviour.NoJavaScript)
                    {
                        response.AddHeader("x-enterprise", Note);
                    }
                    body = string.Empty;
                    break;
                default:
                    response.ContentType = "text/html";
                    body = PageHtml;
                    break;
            }
            var bytes = Encoding.UTF8.GetBytes(body);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }
    }
}
