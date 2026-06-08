using System.Net.Http;

namespace FiftyOne.Pipeline.Cloud.Tests.Common.Helpers
{
    public class TestHttpRequestMessage : HttpRequestMessage
    {
        public TestHttpRequestMessage(HttpMethod method, string testServerRootUrl, string serverRoute)
            : base(method, TestHelpers.GetActualRootUrl(testServerRootUrl) + serverRoute)
        {
        }
    }
}
