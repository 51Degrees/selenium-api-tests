using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.HumanConfidence;

/// <summary>
/// The check a page running the 51Degrees client script is held to once
/// the script's browser tests have run, in whichever browser it is given.
/// </summary>
/// <remarks>
/// The page loads the client script for a resource key, which runs the
/// browser tests and hands the cloud's answer to every function passed to
/// <c>fod.complete</c>. The check passes a function of its own and reads
/// what the script hands it, so it holds a Human Confidence example's page
/// and a page served by these tests to the same thing, whatever each page
/// shows. The answer must carry HumanConfidenceSource as Active and
/// HumanConfidence as a sealed value, which is any value other than the
/// three plain levels, as only the cloud can read it.
/// </remarks>
public static class HumanConfidencePage
{
    /// <summary>
    /// The properties a request names so the answer carries both whatever
    /// the key's own property list holds.
    /// </summary>
    public const string Values =
        "derived.humanconfidence,derived.humanconfidencesource";

    /// <summary>The plain levels, which a sealed value is never.</summary>
    public static readonly IReadOnlyList<string> Levels =
        new[] { "High", "Medium", "Low" };

    /// <summary>
    /// Seconds the page has to load the client script and for the script
    /// to finish its browser tests. Room for a busy runner.
    /// </summary>
    public const int WaitSeconds = 60;

    /// <summary>
    /// Opens <paramref name="page"/>, waits for the client script to
    /// finish, and asserts that its answer has HumanConfidenceSource
    /// Active and HumanConfidence sealed.
    /// </summary>
    /// <param name="driver">A browser started for this check.</param>
    /// <param name="page">The page that runs the client script.</param>
    /// <returns>The derived object the script handed over.</returns>
    public static JObject AssertActiveAfterTheBrowserTests(
        WebDriver driver, Uri page)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(page);

        driver.Navigate().GoToUrl(page);
        try
        {
            new WebDriverWait(driver, TimeSpan.FromSeconds(WaitSeconds))
                .Until(d => (bool)((IJavaScriptExecutor)d).ExecuteScript(
                    "return typeof window.fod === 'object' && " +
                    "window.fod !== null && " +
                    "typeof window.fod.complete === 'function';"));
        }
        catch (WebDriverTimeoutException)
        {
            Assert.Fail(
                $"{page} has no fod.complete after {WaitSeconds} s, so the " +
                "51Degrees client script did not load.");
        }

        driver.Manage().Timeouts().AsynchronousJavaScript =
            TimeSpan.FromSeconds(WaitSeconds);
        string json;
        try
        {
            json = (string)driver.ExecuteAsyncScript(
                "var done = arguments[arguments.length - 1];" +
                "window.fod.complete(function (data) {" +
                "  done(JSON.stringify((data && data.derived) || {}));" +
                "});");
        }
        catch (WebDriverTimeoutException)
        {
            Assert.Fail(
                $"The client script on {page} did not complete within " +
                $"{WaitSeconds} s.");
            throw;
        }

        var derived = JObject.Parse(json);
        var source = derived["humanconfidencesource"];
        Assert.AreEqual(
            "Active",
            source?.Type == JTokenType.String ? (string)source : null,
            $"Once the browser tests have run on {page}, " +
            "humanconfidencesource should be Active, but the answer has " +
            $"{Show(source)}.");

        var value = derived["humanconfidence"];
        var text = value?.Type == JTokenType.String ? (string)value : null;
        Assert.IsFalse(
            string.IsNullOrEmpty(text),
            $"Once the browser tests have run on {page}, humanconfidence " +
            $"should be a sealed value, but the answer has {Show(value)}.");
        Assert.IsFalse(
            Levels.Contains(text),
            $"Once the browser tests have run on {page}, humanconfidence " +
            $"should be a sealed value, but it is the plain level {text}.");
        return derived;
    }

    private static string Show(JToken token) =>
        token == null ? "none" : token.ToString(Newtonsoft.Json.Formatting.None);
}
