using System;
using System.Threading;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Selenium
{
    /// <summary>
    /// Checks that a browser really starts and really runs the page, on
    /// whatever machine the suite is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other browser test needs a cloud, a resource key and an example
    /// application, so a machine that cannot start a browser at all is only
    /// found out in the middle of a test that was trying to check something
    /// else. These need none of that, only a page served from this process, so
    /// CI can put them on each runner it uses and see for itself whether a
    /// browser runs there.
    /// </para>
    /// <para>
    /// They fail rather than skip when no browser can be started, because a
    /// run that quietly skipped them would say nothing, and saying nothing is
    /// the fault these were written for.
    /// </para>
    /// </remarks>
    [TestClass, TestCategory("Browser")]
    public class DriverSmokeTests
    {
        // A page that writes something only a running browser can write, so a
        // driver that connected but never rendered is not mistaken for a pass.
        private const string Page =
            "<!DOCTYPE html><html><head><title>Driver smoke</title></head>"
            + "<body><p id='where'>not run</p><script>"
            + "document.getElementById('where').textContent = "
            + "'ran in ' + navigator.userAgent;"
            + "</script></body></html>";

        private const string WhereElementId = "where";
        private const int PageLoadTimeoutSeconds = 30;

        private string _url;
        private CancellationTokenSource _cts;
        private TestHelpers.ServerListener _server;
        private WebDriver _driver;

        /// <summary>Serves the page from this process.</summary>
        [TestInitialize]
        public void Init()
        {
            _url = $"http://localhost:{TestHelpers.GetRandomUnusedPort()}/";
            _cts = new CancellationTokenSource();
            _server = TestHelpers.SimpleListener(_url, Page, _cts.Token);
        }

        /// <summary>Quits the browser and stops the server.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            _driver?.Quit();
            _driver = null;
            _cts?.Cancel();
            _server?.Listener?.Stop();
            _server?.Listener?.Close();
            _cts?.Dispose();
        }

        /// <summary>Chrome or Chromium starts and runs the page.</summary>
        [TestMethod]
        public void Chrome_RunsThePage()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            _driver = BrowserDrivers.CreateChrome(options);
            AssertPageRan();
        }

        /// <summary>Firefox starts and runs the page.</summary>
        [TestMethod]
        public void Firefox_RunsThePage()
        {
            var options = new FirefoxOptions();
            options.AddArgument("--headless");
            _driver = BrowserDrivers.CreateFirefox(options);
            AssertPageRan();
        }

        /// <summary>
        /// Edge starts and runs the page, where Edge exists at all. Microsoft
        /// publishes no Edge for ARM64 Linux, so there the test says so and
        /// skips instead of failing on something that cannot be fixed here.
        /// </summary>
        [TestMethod]
        public void Edge_RunsThePage()
        {
            var options = new EdgeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            try
            {
                _driver = BrowserDrivers.CreateEdge(options);
            }
            catch (WebDriverException e) when (BrowserDrivers.IsLinuxArm64)
            {
                Assert.Inconclusive(e.Message);
            }
            AssertPageRan();
        }

        /// <summary>
        /// Loads the page and checks the browser ran the script on it.
        /// </summary>
        private void AssertPageRan()
        {
            _driver.Navigate().GoToUrl(_url);
            var deadline = DateTime.UtcNow
                + TimeSpan.FromSeconds(PageLoadTimeoutSeconds);
            string text = null;
            while (DateTime.UtcNow < deadline)
            {
                text = _driver.FindElement(By.Id(WhereElementId)).Text;
                if (text != null && text.StartsWith(
                    "ran in ", StringComparison.Ordinal))
                {
                    Console.WriteLine($"  Browser reported: {text}");
                    return;
                }
                Thread.Sleep(100);
            }
            Assert.Fail(
                "the browser loaded the page but never ran the script on it, "
                + $"as the element reads '{text}'");
        }
    }
}
