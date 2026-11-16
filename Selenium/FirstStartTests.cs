using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Selenium
{
    /// <summary>
    /// Checks that each browser is started once before the first test that
    /// uses it, so that a slow first start is not spent inside a test.
    /// </summary>
    /// <remarks>
    /// <see cref="BrowserDrivers"/> is reached by name, so that a build
    /// without the start once fails these tests rather than the build.
    /// </remarks>
    [TestClass]
    public class FirstStartTests
    {
        private const string StartOnceName = "StartOnce";
        private const string FirstStartsName = "FirstStarts";
        private const string TimeoutName = "FirstStartTimeoutSeconds";

        /// <summary>
        /// A browser's start is run once, however many times it is asked
        /// for, and is given the first start's time limit.
        /// </summary>
        [TestMethod]
        public void StartOnce_StartsEachBrowserOnce()
        {
            var browser = "Stand-in " + Guid.NewGuid();
            var starts = 0;
            var limit = TimeSpan.Zero;
            Action<TimeSpan> start = timeout =>
            {
                starts++;
                limit = timeout;
            };

            StartOnce(browser, start);
            StartOnce(browser, start);

            Assert.AreEqual(1, starts);
            Assert.AreEqual(
                TimeSpan.FromSeconds(TimeoutSeconds()),
                limit);
            StringAssert.StartsWith(
                FirstStarts()[browser],
                $"Started {browser} once before its first test in ");
        }

        /// <summary>
        /// A first start that fails is reported and not thrown, and is not
        /// tried again, so the test that follows meets the failure itself.
        /// </summary>
        [TestMethod]
        public void StartOnce_AStartThatFails_IsReportedAndNotThrown()
        {
            var browser = "Stand-in " + Guid.NewGuid();
            var starts = 0;
            Action<TimeSpan> start = timeout =>
            {
                starts++;
                throw new WebDriverException(
                    "session not created\nsecond line");
            };

            StartOnce(browser, start);
            StartOnce(browser, start);

            Assert.AreEqual(1, starts);
            var line = FirstStarts()[browser];
            StringAssert.StartsWith(
                line, $"{browser} did not finish its first start in ");
            StringAssert.EndsWith(line, "session not created");
        }

        /// <summary>
        /// Creating a Chrome driver starts Chrome once first.
        /// </summary>
        [TestMethod, TestCategory("Browser")]
        public void Chrome_StartsOnceBeforeItsFirstTest()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            AssertStartedOnce(
                "Chrome", () => BrowserDrivers.CreateChrome(options));
        }

        /// <summary>
        /// Creating a Firefox driver starts Firefox once first.
        /// </summary>
        [TestMethod, TestCategory("Browser")]
        public void Firefox_StartsOnceBeforeItsFirstTest()
        {
            var options = new FirefoxOptions();
            options.AddArgument("--headless");
            AssertStartedOnce(
                "Firefox", () => BrowserDrivers.CreateFirefox(options));
        }

        /// <summary>
        /// Creates the browser twice, loading a page each time, writes how
        /// long each took, and checks the first start was made and kept.
        /// </summary>
        /// <param name="browser">Name the first start is kept under.</param>
        /// <param name="create">Creates the driver.</param>
        private static void AssertStartedOnce(
            string browser, Func<WebDriver> create)
        {
            var url =
                $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            using var cts = new CancellationTokenSource();
            var server = TestHelpers.SimpleListener(
                url, "<!DOCTYPE html><title>First start</title>", cts.Token);
            try
            {
                var times = new List<double>();
                for (var i = 0; i < 2; i++)
                {
                    var watch = Stopwatch.StartNew();
                    var driver = create();
                    try
                    {
                        driver.Navigate().GoToUrl(url);
                    }
                    finally
                    {
                        driver.Quit();
                    }
                    times.Add(watch.Elapsed.TotalSeconds);
                }
                var starts = FirstStarts();
                Assert.IsTrue(
                    starts.ContainsKey(browser),
                    $"{browser} was not started once before its first test.");
                Console.WriteLine(starts[browser]);
                Console.WriteLine(FormattableString.Invariant(
                    $"{browser} then started and loaded a page in ")
                    + FormattableString.Invariant(
                        $"{times[0]:0.0} s and {times[1]:0.0} s."));
            }
            finally
            {
                cts.Cancel();
                server?.Listener?.Stop();
                server?.Listener?.Close();
            }
        }

        private static void StartOnce(string browser, Action<TimeSpan> start)
        {
            var method = typeof(BrowserDrivers).GetMethod(
                StartOnceName,
                BindingFlags.Public | BindingFlags.Static,
                new[] { typeof(string), typeof(Action<TimeSpan>) });
            if (method == null)
            {
                Assert.Fail(
                    $"{nameof(BrowserDrivers)} has no {StartOnceName}, so a "
                    + "browser's first start is spent inside a test.");
            }
            method.Invoke(null, new object[] { browser, start });
        }

        private static IReadOnlyDictionary<string, string> FirstStarts()
        {
            var property = typeof(BrowserDrivers).GetProperty(
                FirstStartsName, BindingFlags.Public | BindingFlags.Static);
            if (property == null)
            {
                Assert.Fail(
                    $"{nameof(BrowserDrivers)} has no {FirstStartsName}, so "
                    + "nothing records a browser's first start.");
            }
            return (IReadOnlyDictionary<string, string>)property.GetValue(null);
        }

        private static int TimeoutSeconds()
        {
            var field = typeof(BrowserDrivers).GetField(
                TimeoutName, BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                Assert.Fail(
                    $"{nameof(BrowserDrivers)} has no {TimeoutName}.");
            }
            return (int)field.GetValue(null);
        }
    }
}
