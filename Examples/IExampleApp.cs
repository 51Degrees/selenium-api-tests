using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// A running example web app under test. The browser points at BaseUrl.
    /// Implementations launch a real language example (subprocess) or point at
    /// an already-running URL (CI).
    /// </summary>
    public interface IExampleApp : IAsyncDisposable
    {
        /// <summary>The base URL the browser should navigate to.</summary>
        Uri BaseUrl { get; }

        /// <summary>Starts the example and returns once it is serving HTTP.</summary>
        Task StartAsync(ExampleAppOptions options, CancellationToken ct);
    }

    /// <summary>
    /// Inputs the runner passes so the example talks to the cloud under test.
    /// </summary>
    /// <param name="Port">Port the example must listen on.</param>
    /// <param name="CloudEndpoint">Cloud base URL the example points its CloudRequestEngine at.</param>
    /// <param name="ResourceKey">A resource key the cloud accepts.</param>
    /// <param name="ExtraEnv">Additional environment variables for the example process.</param>
    public sealed record ExampleAppOptions(
        int Port,
        Uri CloudEndpoint,
        string ResourceKey,
        IReadOnlyDictionary<string, string> ExtraEnv);
}
