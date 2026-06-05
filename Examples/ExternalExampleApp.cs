using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// Points at an example that is already running (CI brings it up).
    /// Selected when EXAMPLE_URL is set.
    /// </summary>
    public sealed class ExternalExampleApp : IExampleApp
    {
        /// <inheritdoc/>
        public Uri BaseUrl { get; }

        /// <summary>Creates a provider for an already-running example.</summary>
        public ExternalExampleApp(Uri baseUrl) => BaseUrl = baseUrl;

        /// <summary>
        /// No-op — the example is already running. Whoever started it must point it at
        /// the test's cloud and resource key; the options here are not applied.
        /// </summary>
        public Task StartAsync(ExampleAppOptions options, CancellationToken ct) => Task.CompletedTask;

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
