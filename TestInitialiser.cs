using FiftyOne.Pipeline.Cloud.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests
{
    /// <summary>
    /// Assembly-level setup. The cloud is always external; its URL comes from
    /// the test configuration (TEST_CONFIG_FILE -> cloud_root_url).
    /// </summary>
    [TestClass]
    public class TestInitialiser
    {
        /// <summary>
        /// Base URL of the cloud under test. Empty when no external cloud is
        /// configured.
        /// </summary>
        public static string CloudServerUrl;

        /// <summary>Reads the external cloud URL from the test configuration.</summary>
        [AssemblyInitialize]
        public static void TestInitialise(TestContext testContext)
        {
            CloudServerUrl = TestConfig.Instance().RootUrl;
        }
    }
}
