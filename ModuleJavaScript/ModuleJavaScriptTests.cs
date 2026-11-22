using System;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Examples;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.ModuleJavaScript;

/// <summary>
/// Checks a web server module's JavaScript example, such as the NGINX or
/// Varnish module's, in Chrome and Firefox. The module's CI serves the
/// example's page with the module running and names the page in
/// _51DEGREES_EXAMPLE_URL. See <see cref="ModuleJavaScriptPage"/> for what the page
/// must do.
/// </summary>
[TestClass, TestCategory("ModuleJavaScript")]
public class ModuleJavaScriptTests
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
    /// The module's JavaScript runs in Chrome and the page shows the width
    /// the module detected from it.
    /// </summary>
    [TestMethod]
    public void Chrome_RunsTheModulesJavaScript()
    {
        var page = ExamplePage();
        _driver = ModuleJavaScriptPage.StartChrome();
        ModuleJavaScriptPage.AssertRanTheModulesJavaScript(_driver, page);
    }

    /// <summary>
    /// The module's JavaScript runs in Firefox and the page shows the width
    /// the module detected from it.
    /// </summary>
    [TestMethod]
    public void Firefox_RunsTheModulesJavaScript()
    {
        var page = ExamplePage();
        _driver = BrowserDrivers.CreateFirefox(new FirefoxOptions());
        ModuleJavaScriptPage.AssertRanTheModulesJavaScript(_driver, page);
    }

    /// <summary>
    /// The page _51DEGREES_EXAMPLE_URL names. A run without it fails, naming the
    /// variable, because there is no example this suite could start in its
    /// place.
    /// </summary>
    private static Uri ExamplePage()
    {
        var value = Environment.GetEnvironmentVariable(
            ExampleApps.ExampleUrlVar);
        if (string.IsNullOrEmpty(value))
        {
            Assert.Fail(
                $"{ExampleApps.ExampleUrlVar} is not set. Set it to the "
                + "address of the module's JavaScript example page, served "
                + "with the module running.");
        }
        return new Uri(value);
    }
}
