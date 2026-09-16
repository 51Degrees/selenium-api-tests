using FiftyOne.Pipeline.Cloud.Tests.Common;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests
{
    /// <summary>
    /// Where the tests find the cloud under test.
    /// </summary>
    /// <remarks>
    /// There is no assembly-level setup. The cloud URL used to be read once
    /// for the whole run, which failed every test, including Contract tests
    /// against an example that was already running, whenever CLOUD_ROOT_URL
    /// was unset. It is now read by the tests that talk to the cloud
    /// directly, and only they fail when it is missing.
    /// </remarks>
    public static class TestInitialiser
    {
        /// <summary>
        /// Base URL of the cloud under test, from CLOUD_ROOT_URL. Throws an
        /// <see cref="System.InvalidOperationException"/> naming the variable
        /// when it is not set.
        /// </summary>
        public static string CloudServerUrl => TestConfig.Instance().RootUrl;
    }
}
