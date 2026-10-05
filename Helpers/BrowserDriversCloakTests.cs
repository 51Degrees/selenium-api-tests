using System;
using System.Collections.Generic;
using System.IO;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Tests
{
    /// <summary>
    /// Checks the rules for attaching to CloakBrowser. No browser is started
    /// and none has to be running, so these run anywhere.
    /// </summary>
    [TestClass]
    public class BrowserDriversCloakTests
    {
        // What the DevTools port of CloakBrowser 0.5.12 serves at
        // /json/version, less the fields that are not read.
        private const string VersionDocument =
            "{\"Browser\": \"Chrome/146.0.7680.177\", "
            + "\"Protocol-Version\": \"1.3\", "
            + "\"webSocketDebuggerUrl\": \"ws://127.0.0.1:9222/devtools/"
            + "browser/00000000-0000-0000-0000-000000000000\"}";

        private string _directory;
        private string _driverFile;

        /// <summary>
        /// Makes a directory holding a file named like a driver.
        /// </summary>
        [TestInitialize]
        public void Init()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "51d-cloak-driver-" + Guid.NewGuid());
            Directory.CreateDirectory(_directory);
            _driverFile = Path.Combine(_directory, "chromedriver");
            File.WriteAllText(_driverFile, string.Empty);
        }

        /// <summary>Removes the directory.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (_directory != null && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        /// <summary>
        /// An unset variable and an empty one both mean there is no browser
        /// to attach to, as a CI script that exports an unset variable
        /// produces an empty one.
        /// </summary>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        public void CloakDebuggerAddress_UnsetOrEmpty_IsNull(string value)
        {
            Assert.IsNull(BrowserDrivers.CloakDebuggerAddress(Lookup(
                BrowserDrivers.CloakDebuggerAddressVariable, value)));
        }

        /// <summary>The address is read from the variable.</summary>
        [TestMethod]
        public void CloakDebuggerAddress_Set_IsTheAddress()
        {
            Assert.AreEqual(
                "127.0.0.1:9222",
                BrowserDrivers.CloakDebuggerAddress(Lookup(
                    BrowserDrivers.CloakDebuggerAddressVariable,
                    " 127.0.0.1:9222 ")));
        }

        /// <summary>
        /// With no address there is nothing to attach to, and the failure
        /// names the variable that would supply one.
        /// </summary>
        [TestMethod]
        public void CreateCloak_NoAddress_NamesTheVariable()
        {
            var e = Assert.ThrowsExactly<WebDriverException>(
                () => BrowserDrivers.CreateCloak(
                    Lookup(new Dictionary<string, string>()),
                    new ChromeOptions()));

            Assert.AreEqual(
                BrowserDrivers.CloakNotConfiguredMessage, e.Message);
            StringAssert.Contains(
                e.Message, BrowserDrivers.CloakDebuggerAddressVariable);
        }

        /// <summary>
        /// An address nothing listens at fails before any driver is looked
        /// for, and the failure names the variable and the address.
        /// </summary>
        [TestMethod]
        public void CreateCloak_NothingAtTheAddress_NamesTheVariable()
        {
            var address = $"127.0.0.1:{TestHelpers.GetRandomUnusedPort()}";

            var e = Assert.ThrowsExactly<WebDriverException>(
                () => BrowserDrivers.CreateCloak(
                    Lookup(
                        BrowserDrivers.CloakDebuggerAddressVariable, address),
                    new ChromeOptions()));

            StringAssert.Contains(
                e.Message, BrowserDrivers.CloakDebuggerAddressVariable);
            StringAssert.Contains(e.Message, address);
        }

        /// <summary>
        /// The major version comes from the browser itself, so the driver
        /// asked for is always the one that browser needs.
        /// </summary>
        [TestMethod]
        public void CloakMajorVersion_ReadsTheBrowserField()
        {
            Assert.AreEqual(
                "146", BrowserDrivers.CloakMajorVersion(VersionDocument));
        }

        /// <summary>
        /// An answer that is not a browser's says so and names the variable,
        /// which is what a wrong port gives.
        /// </summary>
        [TestMethod]
        [DataRow("{}")]
        [DataRow("{\"Browser\": \"Chrome\"}")]
        [DataRow("{\"Browser\": 146}")]
        [DataRow("[]")]
        [DataRow("<html>not a browser</html>")]
        [DataRow("")]
        [DataRow(null)]
        public void CloakMajorVersion_NotABrowser_NamesTheVariable(
            string document)
        {
            var e = Assert.ThrowsExactly<WebDriverException>(
                () => BrowserDrivers.CloakMajorVersion(document));

            StringAssert.Contains(
                e.Message, BrowserDrivers.CloakDebuggerAddressVariable);
        }

        /// <summary>
        /// A variable naming the directory the driver is in finds it.
        /// </summary>
        [TestMethod]
        public void FindNamedDriver_ReadsDirectoryFromVariable()
        {
            Assert.AreEqual(
                _driverFile,
                BrowserDrivers.FindNamedDriver(
                    Lookup(BrowserDrivers.CloakDriverVariable, _directory),
                    BrowserDrivers.CloakDriverVariable,
                    BrowserDrivers.ChromeDriverNames));
        }

        /// <summary>A variable naming the driver itself finds it.</summary>
        [TestMethod]
        public void FindNamedDriver_ReadsFileFromVariable()
        {
            Assert.AreEqual(
                _driverFile,
                BrowserDrivers.FindNamedDriver(
                    Lookup(BrowserDrivers.CloakDriverVariable, _driverFile),
                    BrowserDrivers.CloakDriverVariable,
                    BrowserDrivers.ChromeDriverNames));
        }

        /// <summary>
        /// A chromedriver on the path is not used, as it is the one for the
        /// machine's own Chrome and not for CloakBrowser's version.
        /// </summary>
        [TestMethod]
        public void FindNamedDriver_DoesNotSearchThePath()
        {
            Assert.IsNull(
                BrowserDrivers.FindNamedDriver(
                    Lookup("PATH", _directory),
                    BrowserDrivers.CloakDriverVariable,
                    BrowserDrivers.ChromeDriverNames));
        }

        /// <summary>
        /// The reason a Cloak test gives for being skipped names the
        /// variable to set.
        /// </summary>
        [TestMethod]
        public void RequiresCloak_SkipReason_NamesTheVariable()
        {
            StringAssert.Contains(
                new RequiresCloakAttribute().IgnoreMessage,
                BrowserDrivers.CloakDebuggerAddressVariable);
        }

        /// <summary>Reads one variable in place of the environment.</summary>
        /// <param name="name">Name of the variable.</param>
        /// <param name="value">Its value, or null for unset.</param>
        /// <returns>A lookup holding only that variable.</returns>
        private static Func<string, string> Lookup(string name, string value) =>
            Lookup(new Dictionary<string, string> { { name, value } });

        /// <summary>
        /// Reads from a dictionary in place of the environment.
        /// </summary>
        /// <param name="values">Variables and their values.</param>
        /// <returns>A lookup over those values.</returns>
        private static Func<string, string> Lookup(
            IReadOnlyDictionary<string, string> values) =>
            name => values.TryGetValue(name, out var value) ? value : null;
    }
}
