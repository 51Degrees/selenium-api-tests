using System;
using System.Globalization;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.ModuleJavaScript;

/// <summary>
/// The check a web server module's JavaScript example is held to, in
/// whichever browser it is given.
/// </summary>
/// <remarks>
/// <para>
/// The example's page loads the JavaScript the module serves for the
/// ScreenPixelsWidthJavascript property, which writes the browser's screen
/// width into the <see cref="WidthCookie"/> cookie. The page then sends the
/// cookie to the module, which detects the width from it, and writes the
/// width the module answered into the element <see cref="WidthElementId"/>.
/// </para>
/// <para>
/// So the cookie must hold the browser's own screen width, which only the
/// module's JavaScript writes, and the page must show that same width, which
/// the module only answers when it used the cookie. Without the JavaScript
/// the page still shows a value, being the one the data file holds for the
/// User-Agent, so a page that merely shows something proves nothing.
/// </para>
/// </remarks>
public static class ModuleJavaScriptPage
{
    /// <summary>Cookie the module's JavaScript writes the width into.</summary>
    public const string WidthCookie = "51D_ScreenPixelsWidth";

    /// <summary>Element the page writes the detected width into.</summary>
    public const string WidthElementId = "widthvalue";

    /// <summary>
    /// Element the page writes the module's note into, when the module says
    /// the data file has no JavaScript for the property.
    /// </summary>
    public const string NoteElementId = "enterprise";

    /// <summary>
    /// Seconds the page has to show the width once it has loaded. The page
    /// makes one request to the module, so this is room for a busy runner.
    /// </summary>
    public const int PageWaitSeconds = 60;

    private const string HeadlessChromeToken = "HeadlessChrome/";
    private const string ChromeToken = "Chrome/";

    /// <summary>
    /// Starts Chrome sending the User-Agent the same Chrome sends when it
    /// has a window. The data file holds no JavaScript for a User-Agent
    /// naming HeadlessChrome, so the module would serve headless Chrome
    /// none. The User-Agent is read from the browser rather than written
    /// here, so it is always one a real Chrome sends.
    /// </summary>
    /// <returns>A driver, which the caller quits.</returns>
    public static WebDriver StartChrome()
    {
        string userAgent;
        var probe = BrowserDrivers.CreateChrome(new ChromeOptions());
        try
        {
            userAgent = (string)probe.ExecuteScript(
                "return navigator.userAgent;");
        }
        finally
        {
            probe.Quit();
        }
        var options = new ChromeOptions();
        options.AddArgument("--user-agent=" + userAgent.Replace(
            HeadlessChromeToken, ChromeToken, StringComparison.Ordinal));
        return BrowserDrivers.CreateChrome(options);
    }

    /// <summary>
    /// Opens <paramref name="page"/> and asserts that the module's
    /// JavaScript ran in the browser and that the page shows the width the
    /// module detected from it.
    /// </summary>
    /// <param name="driver">A browser started for this check.</param>
    /// <param name="page">The example's page.</param>
    public static void AssertRanTheModulesJavaScript(
        WebDriver driver, Uri page)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(page);

        driver.Navigate().GoToUrl(page);
        string shown = null;
        try
        {
            shown = new WebDriverWait(
                    driver, TimeSpan.FromSeconds(PageWaitSeconds))
                .Until(d => TextOf(d, WidthElementId) is { Length: > 0 } text
                    ? text
                    : null);
        }
        catch (WebDriverTimeoutException)
        {
            Assert.Fail(
                $"{page} showed no width in the element '{WidthElementId}' "
                + $"within {PageWaitSeconds} s, so the page had no answer "
                + "from the module." + NoteOf(driver));
        }

        var screenWidth = Convert.ToInt64(
                driver.ExecuteScript("return screen.width;"),
                CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);
        var cookie = driver.Manage().Cookies.GetCookieNamed(WidthCookie);
        Assert.IsNotNull(
            cookie,
            $"{page} has no {WidthCookie} cookie, so the module's "
            + "JavaScript did not run in the browser." + NoteOf(driver));
        Assert.AreEqual(
            screenWidth,
            cookie.Value,
            $"The {WidthCookie} cookie holds '{cookie.Value}', where the "
            + $"browser's screen is {screenWidth} wide, so the cookie was not "
            + "written by the module's JavaScript.");
        Assert.AreEqual(
            screenWidth,
            shown,
            $"{page} shows the width '{shown}', where the browser's screen "
            + $"is {screenWidth} wide, so the module did not detect the width "
            + "from the evidence its JavaScript gave." + NoteOf(driver));
        Console.WriteLine(
            $"  {page} shows the width {shown}, the browser's own, from the "
            + "module's JavaScript.");
    }

    /// <summary>
    /// Text of the element with <paramref name="id"/>, or null when there is
    /// no such element.
    /// </summary>
    private static string TextOf(ISearchContext context, string id)
    {
        var found = context.FindElements(By.Id(id));
        return found.Count == 0 ? null : found[0].Text.Trim();
    }

    /// <summary>
    /// The module's note as the page shows it, for a failure message, as it
    /// says why the module served no JavaScript.
    /// </summary>
    private static string NoteOf(WebDriver driver)
    {
        var note = TextOf(driver, NoteElementId);
        return string.IsNullOrEmpty(note)
            ? string.Empty
            : $" The page says: {note}";
    }
}
