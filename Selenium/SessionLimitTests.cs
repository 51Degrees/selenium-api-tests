using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Selenium;

/// <summary>
/// Checks that every session a test starts is given the first start's time
/// limit rather than the driver's 60 second default, because a later start
/// can take as long as a first one. Writes how long each start took, so each
/// runner's start times are in its log.
/// </summary>
[TestClass, TestCategory("Browser")]
public class SessionLimitTests
{
    /// <summary>Sessions each test starts and measures.</summary>
    private const int Sessions = 3;

    /// <summary>
    /// Field of Selenium's <see cref="HttpCommandExecutor"/> that holds the
    /// time limit of each command, read because Selenium offers no public
    /// way to read it.
    /// </summary>
    private const string LimitFieldName = "serverResponseTimeout";

    /// <summary>
    /// Every Chrome session has at least the first start's limit.
    /// </summary>
    [TestMethod]
    public void Chrome_EverySessionHasTheFirstStartLimit()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless");
        AssertEverySessionLimit(
            "Chrome", () => BrowserDrivers.CreateChrome(options));
    }

    /// <summary>
    /// Every Firefox session has at least the first start's limit.
    /// </summary>
    [TestMethod]
    public void Firefox_EverySessionHasTheFirstStartLimit()
    {
        var options = new FirefoxOptions();
        options.AddArgument("--headless");
        AssertEverySessionLimit(
            "Firefox", () => BrowserDrivers.CreateFirefox(options));
    }

    /// <summary>
    /// Starts <see cref="Sessions"/> sessions, writes how long each start
    /// took, and checks each session's limit.
    /// </summary>
    /// <param name="browser">Name of the browser, for the output.</param>
    /// <param name="create">Starts a session.</param>
    private static void AssertEverySessionLimit(
        string browser, Func<WebDriver> create)
    {
        var expected = TimeSpan.FromSeconds(
            BrowserDrivers.FirstStartTimeoutSeconds);
        var times = new List<string>();
        var limits = new List<TimeSpan>();
        for (var i = 0; i < Sessions; i++)
        {
            var watch = Stopwatch.StartNew();
            var driver = create();
            times.Add(Seconds(watch.Elapsed));
            try
            {
                limits.Add(Limit(driver));
            }
            finally
            {
                driver.Quit();
            }
        }
        if (BrowserDrivers.FirstStarts.TryGetValue(browser, out var first))
        {
            Console.WriteLine(first);
        }
        Console.WriteLine(
            $"{browser} sessions started in {string.Join(", ", times)} s, "
            + "the first of them including the browser's first start when "
            + "this test was the first to use it.");
        for (var i = 0; i < limits.Count; i++)
        {
            Assert.IsTrue(
                limits[i] >= expected,
                $"{browser} session {i + 1} has a limit of "
                + $"{Seconds(limits[i])} s, under the first start's "
                + $"{Seconds(expected)} s.");
        }
    }

    /// <summary>
    /// The time limit Selenium gives each command of
    /// <paramref name="driver"/>.
    /// </summary>
    /// <param name="driver">A driver just created.</param>
    /// <returns>The limit.</returns>
    private static TimeSpan Limit(WebDriver driver)
    {
        var executor = driver.CommandExecutor;
        if (executor is DriverServiceCommandExecutor service)
        {
            executor = service.HttpExecutor;
        }
        var field = typeof(HttpCommandExecutor).GetField(
            LimitFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null || !(executor is HttpCommandExecutor))
        {
            Assert.Fail(
                $"The Selenium in use keeps no {LimitFieldName} in its "
                + $"{nameof(HttpCommandExecutor)}, so {nameof(Limit)} needs "
                + "changing to read the limit where it is now.");
        }
        return (TimeSpan)field.GetValue(executor);
    }

    private static string Seconds(TimeSpan time) =>
        time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
}
