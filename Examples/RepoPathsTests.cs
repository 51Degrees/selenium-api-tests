using System.IO;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// Tests for <see cref="RepoPaths"/>.
    /// </summary>
    [TestClass]
    public class RepoPathsTests
    {
        /// <summary>
        /// The resolved siblings root must be an existing directory.
        /// </summary>
        [TestMethod]
        public void SiblingsRoot_IsAnExistingDirectory()
        {
            Assert.IsTrue(Directory.Exists(RepoPaths.SiblingsRoot),
                $"SiblingsRoot '{RepoPaths.SiblingsRoot}' should be an existing directory");
        }
    }
}
