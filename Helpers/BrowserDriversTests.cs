using System;
using System.Collections.Generic;
using System.IO;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Tests
{
    /// <summary>
    /// Checks the rules for finding a driver the machine provides. No browser
    /// is started, so these run anywhere.
    /// </summary>
    [TestClass]
    public class BrowserDriversTests
    {
        private string _directory;
        private string _driverFile;

        /// <summary>Makes a directory holding a file named like a driver.</summary>
        [TestInitialize]
        public void Init()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "51d-driver-" + Guid.NewGuid());
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
        /// A variable naming the directory the driver is in finds the driver,
        /// which is the shape GitHub's runner images use.
        /// </summary>
        [TestMethod]
        public void FindDriver_ReadsDirectoryFromVariable()
        {
            Assert.AreEqual(
                _driverFile,
                BrowserDrivers.FindDriver(
                    Lookup(new Dictionary<string, string>
                    {
                        { BrowserDrivers.ChromeDriverVariable, _directory },
                    }),
                    BrowserDrivers.ChromeDriverVariable,
                    BrowserDrivers.ChromeDriverNames));
        }

        /// <summary>A variable naming the driver itself finds the driver.</summary>
        [TestMethod]
        public void FindDriver_ReadsFileFromVariable()
        {
            Assert.AreEqual(
                _driverFile,
                BrowserDrivers.FindDriver(
                    Lookup(new Dictionary<string, string>
                    {
                        { BrowserDrivers.ChromeDriverVariable, _driverFile },
                    }),
                    BrowserDrivers.ChromeDriverVariable,
                    BrowserDrivers.ChromeDriverNames));
        }

        /// <summary>
        /// With no variable set the path is searched, so a machine that just
        /// has a driver installed needs no configuration.
        /// </summary>
        [TestMethod]
        public void FindDriver_FallsBackToThePath()
        {
            Assert.AreEqual(
                _driverFile,
                BrowserDrivers.FindDriver(
                    Lookup(new Dictionary<string, string>
                    {
                        { "PATH", _directory },
                    }),
                    BrowserDrivers.ChromeDriverVariable,
                    BrowserDrivers.ChromeDriverNames));
        }

        /// <summary>
        /// A variable set to a directory holding no driver does not stop the
        /// path being searched, which is what an ARM64 runner does with
        /// CHROMEWEBDRIVER left empty.
        /// </summary>
        [TestMethod]
        public void FindDriver_SearchesThePathWhenTheVariableIsWrong()
        {
            var empty = Path.Combine(_directory, "empty");
            Directory.CreateDirectory(empty);
            Assert.AreEqual(
                _driverFile,
                BrowserDrivers.FindDriver(
                    Lookup(new Dictionary<string, string>
                    {
                        { BrowserDrivers.ChromeDriverVariable, empty },
                        { "PATH", _directory },
                    }),
                    BrowserDrivers.ChromeDriverVariable,
                    BrowserDrivers.ChromeDriverNames));
        }

        /// <summary>
        /// Nothing anywhere gives null, which is what sends the caller on to
        /// Selenium Manager.
        /// </summary>
        [TestMethod]
        public void FindDriver_ReturnsNullWhenNothingProvidesOne()
        {
            Assert.IsNull(
                BrowserDrivers.FindDriver(
                    Lookup(new Dictionary<string, string>()),
                    BrowserDrivers.GeckoDriverVariable,
                    BrowserDrivers.GeckoDriverNames));
        }

        /// <summary>
        /// Every driver name is looked for, so a Windows machine holding
        /// chromedriver.exe is found as readily as a Linux one.
        /// </summary>
        [TestMethod]
        public void FindDriver_FindsEveryNameItIsGiven()
        {
            var windowsName = Path.Combine(_directory, "geckodriver.exe");
            File.WriteAllText(windowsName, string.Empty);
            Assert.AreEqual(
                windowsName,
                BrowserDrivers.FindDriver(
                    Lookup(new Dictionary<string, string>
                    {
                        { BrowserDrivers.GeckoDriverVariable, _directory },
                    }),
                    BrowserDrivers.GeckoDriverVariable,
                    BrowserDrivers.GeckoDriverNames));
        }

        /// <summary>Reads from a dictionary in place of the environment.</summary>
        /// <param name="values">Variables and their values.</param>
        /// <returns>A lookup over those values.</returns>
        private static Func<string, string> Lookup(
            IReadOnlyDictionary<string, string> values) =>
            name => values.TryGetValue(name, out var value) ? value : null;
    }
}
