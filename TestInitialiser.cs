using FiftyOne.Pipeline.Cloud.Tests.Common;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests
{
    /// <summary>
    /// Where the tests find the cloud under test.
    /// </summary>
    /// <remarks>
    /// There is no assembly-level setup. The cloud URL is read by the tests
    /// that talk to the cloud directly.
    /// </remarks>
    public static class TestInitialiser
    {
        /// <summary>
        /// Base URL of the cloud under test, from _51DEGREES_CLOUD_ENDPOINT
        /// without its api/v4 path, or the public service's when that is
        /// unset.
        /// </summary>
        public static string CloudServerUrl => TestConfig.Instance().RootUrl;
    }
}
