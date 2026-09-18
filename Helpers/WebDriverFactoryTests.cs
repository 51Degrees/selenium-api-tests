using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers
{
    /// <summary>
    /// Covers the part of <see cref="WebDriverFactory"/> that decides how a
    /// browser is launched. No browser is started here - the driver process
    /// itself is exercised by the Selenium tests, but the arguments it is given
    /// are what actually fixed the CI timeouts, so they are worth asserting
    /// somewhere that runs in milliseconds.
    /// </summary>
    [TestClass, TestCategory("CloudInternal")]
    public class WebDriverFactoryTests
    {
        private static readonly string[] StabilityArguments =
        {
            "--no-sandbox",
            "--disable-gpu",
            "--disable-dev-shm-usage",
        };

        /// <summary>
        /// Chrome is the browser most of the suite runs on, and the one that
        /// was dying on constrained runners.
        /// </summary>
        [TestMethod]
        public void PrepareOptions_Chrome_AddsHeadlessAndStabilityArguments()
        {
            var options = new ChromeOptions();

            WebDriverFactory.PrepareOptions(options);

            CollectionAssert.Contains(options.Arguments.ToList(), "--headless");
            foreach (var argument in StabilityArguments)
            {
                CollectionAssert.Contains(options.Arguments.ToList(), argument,
                    $"{argument} was not applied to ChromeOptions");
            }
        }

        /// <summary>
        /// Edge is the browser the original failure was reported against, so it
        /// must get the same treatment as Chrome rather than being forgotten.
        /// </summary>
        [TestMethod]
        public void PrepareOptions_Edge_AddsHeadlessAndStabilityArguments()
        {
            var options = new EdgeOptions();

            WebDriverFactory.PrepareOptions(options);

            CollectionAssert.Contains(options.Arguments.ToList(), "--headless");
            foreach (var argument in StabilityArguments)
            {
                CollectionAssert.Contains(options.Arguments.ToList(), argument,
                    $"{argument} was not applied to EdgeOptions");
            }
        }

        /// <summary>
        /// Callers are allowed to have set an argument themselves; passing it to
        /// the browser twice is at best noise and at worst a launch failure.
        /// </summary>
        [TestMethod]
        public void PrepareOptions_ArgumentAlreadyPresent_IsNotDuplicated()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");

            WebDriverFactory.PrepareOptions(options);

            Assert.AreEqual(1, options.Arguments.Count(a => a == "--headless"),
                "--headless was added a second time");
            Assert.AreEqual(1, options.Arguments.Count(a => a == "--no-sandbox"),
                "--no-sandbox was added a second time");
        }

        /// <summary>
        /// Arguments a caller set for its own reasons must survive.
        /// </summary>
        [TestMethod]
        public void PrepareOptions_KeepsCallerArguments()
        {
            var options = new ChromeOptions();
            options.AddArgument("--disable-features=UserAgentClientHint");

            WebDriverFactory.PrepareOptions(options);

            CollectionAssert.Contains(options.Arguments.ToList(),
                "--disable-features=UserAgentClientHint");
        }

        /// <summary>
        /// The stability arguments are Chromium arguments. Handing them to
        /// geckodriver would be putting unknown flags in front of Firefox, which
        /// currently runs green and must stay that way.
        /// </summary>
        [TestMethod]
        public void PrepareOptions_Firefox_GetsHeadlessButNoChromiumArguments()
        {
            var options = new FirefoxOptions();

            WebDriverFactory.PrepareOptions(options);

            var arguments = FirefoxArguments(options);
            CollectionAssert.Contains(arguments, "--headless");
            foreach (var argument in StabilityArguments)
            {
                CollectionAssert.DoesNotContain(arguments, argument,
                    $"{argument} is a Chromium argument and must not reach Firefox");
            }
        }

        /// <summary>
        /// Every test page is served over plain HTTP or a self-signed
        /// certificate, so this has to be on however the caller built the
        /// options.
        /// </summary>
        [TestMethod]
        public void PrepareOptions_AcceptsInsecureCertificates()
        {
            var options = new ChromeOptions();

            WebDriverFactory.PrepareOptions(options);

            Assert.IsTrue(options.AcceptInsecureCertificates);
        }

        /// <summary>
        /// A browser nobody has configured should fail loudly rather than launch
        /// without the hardening this class exists to apply.
        /// </summary>
        [TestMethod]
        public void PrepareOptions_UnsupportedOptions_Throws()
        {
            Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(
                () => WebDriverFactory.PrepareOptions(new UnsupportedOptions()));
        }

        /// <summary>
        /// Selenium does not expose FirefoxOptions.Arguments, so read them back
        /// off the capabilities the driver would actually be started with.
        /// </summary>
        /// <param name="options">Options to inspect.</param>
        /// <returns>The Firefox command line arguments.</returns>
        private static List<string> FirefoxArguments(FirefoxOptions options)
        {
            var firefoxOptions = options.ToCapabilities()
                .GetCapability("moz:firefoxOptions");
            var arguments = ((Dictionary<string, object>)firefoxOptions)["args"];
            return ((IEnumerable<object>)arguments)
                .Select(a => a.ToString())
                .ToList();
        }

        /// <summary>
        /// Stand-in for a browser the factory does not know about.
        /// </summary>
        private sealed class UnsupportedOptions : DriverOptions
        {
            public override ICapabilities ToCapabilities() => null;
        }
    }
}
