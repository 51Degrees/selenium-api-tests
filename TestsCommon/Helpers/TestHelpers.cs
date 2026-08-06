using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Cloud.Tests.Common.Helpers
{
    /// <summary>
    /// Helpers for cloud integration tests.
    /// </summary>
    public static class TestHelpers
    {

        /// <summary>
        /// Creates a simple HttpListener.
        /// </summary>
        public static HttpListener SimpleListener(string url, CancellationToken token)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            Task listenTask = new HttpServer(listener).HandleIncomingConnections(token);
            listenTask.GetAwaiter();
            return listener;
        }

        /// <summary>
        /// Class containing a listener, and the server which is serving the
        /// requests.
        /// </summary>
        public class ServerListener
        {
            public HttpListener Listener { get; }
            public HttpServer Server { get; }
            public ServerListener(HttpListener listener, HttpServer server)
            {
                Listener = listener;
                Server = server;
            }
        }

        /// <summary>
        /// Creates a simple HttpListener.
        /// </summary>
        public static ServerListener SimpleListener(string url, string pageData, CancellationToken token)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            var server = new HttpServer(listener, pageData);
            Task listenTask = server.HandleIncomingConnections(token);
            listenTask.GetAwaiter();
            return new ServerListener(listener, server);
        }

        /// <summary>
        /// Creates an HttpListener that serves <paramref name="pageData"/> on
        /// non-/api/ paths and proxies /api/ requests to <paramref name="cloudUrl"/>.
        /// </summary>
        public static ServerListener ProxyingListener(
            string clientUrl,
            string cloudUrl,
            string pageData,
            CancellationToken token,
            IReadOnlyDictionary<string, string> extraResponseHeaders = null)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(clientUrl);
            listener.Start();

            var server = new HttpServer(listener, pageData, cloudUrl, extraResponseHeaders);
            Task listenTask = server.HandleIncomingConnections(token);
            listenTask.GetAwaiter();
            return new ServerListener(listener, server);
        }

        /// <summary>
        /// Creates an HttpListener that forwards every request to
        /// <paramref name="targetUrl"/> and adds
        /// <paramref name="extraResponseHeaders"/> to each response.
        /// </summary>
        public static ServerListener ReverseProxyListener(
            string clientUrl,
            string targetUrl,
            CancellationToken token,
            IReadOnlyDictionary<string, string> extraResponseHeaders)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(clientUrl);
            listener.Start();

            var server = new HttpServer(listener, targetUrl, extraResponseHeaders);
            Task listenTask = server.HandleIncomingConnections(token);
            listenTask.GetAwaiter();
            return new ServerListener(listener, server);
        }

        /// <summary>
        /// Creates an HttpListener that serves <paramref name="pageData"/>
        /// for page paths and proxies the 51Degrees script and pipeline
        /// endpoints to <paramref name="exampleUrl"/> without caching.
        /// <paramref name="jsonEndpointPath"/> is the path the client-side
        /// script posts refreshed evidence to, which differs per web
        /// integration, so the caller supplies the one its example uses.
        /// </summary>
        public static ServerListener ExampleProxyListener(
            string clientUrl,
            string exampleUrl,
            string pageData,
            string jsonEndpointPath,
            CancellationToken token)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add(clientUrl);
            listener.Start();

            var server = new HttpServer(listener, pageData)
            {
                ProxyRoutes = new Dictionary<string, string>
                {
                    ["/51Degrees.core.js"] = exampleUrl,
                    [jsonEndpointPath] = exampleUrl,
                },
                ProxiedHeaderOverrides = new Dictionary<string, string>
                {
                    ["Cache-Control"] = "no-store",
                },
            };
            Task listenTask = server.HandleIncomingConnections(token);
            listenTask.GetAwaiter();
            return new ServerListener(listener, server);
        }

        /// <summary>
        /// Start a new TcpListener with the port as 0 so that the OS assigns an
        /// available port. Record this then close the listener. This ensures
        /// that a 'randomly' generated port number is free.
        /// </summary>
        public static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        /// <summary>
        /// Starts a geo server on a random unused port and configures the
        /// cloud pipeline to use that port via an environment variable.
        /// </summary>
        /// <param name="pageData">The response body to return from the geo server.</param>
        /// <param name="token">Cancellation token to stop the server.</param>
        /// <returns>The started <see cref="ServerListener"/>.</returns>
        public static ServerListener StartGeoServer(string pageData, CancellationToken token)
        {
            var geoPort = GetRandomUnusedPort();
            Environment.SetEnvironmentVariable(
                "PipelineOptions__Elements__CloudNominatimElement__BuildParameters__Url",
                $"http://localhost:{geoPort}/");
            return SimpleListener($"http://localhost:{geoPort}/", pageData, token);
        }

        /// <summary>
        /// Get the actual Root Url used for testing.
        /// If test environment is configured to use URL from configuration
        /// file, then return the URL specified from configuration file. Else
        /// return the AspNet Test Server URL being passed in.
        /// </summary>
        public static string GetActualRootUrl(string testServerRootUrl)
        {
            var config = TestConfig.Instance();
            if (String.IsNullOrEmpty(config.RootUrl))
            {
                return testServerRootUrl;
            }
            else
            {
                return config.RootUrl;
            }
        }
    }
}
