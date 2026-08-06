using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Cloud.Tests.Common.Helpers
{
    /// <summary>
    /// An Http server to use during testing.
    /// </summary>
    public class HttpServer
    {
        /// <summary>
        /// The number of requests made to the server since the
        /// reset method was called.
        /// </summary>
        public int RequestCount { get; private set; } = 0;

        /// <summary>
        /// The HttpListener used to monitor for requests.
        /// </summary>
        public HttpListener Listener { get; private set; }

        /// <summary>
        /// The content returned by the HttpServer.
        /// </summary>
        public string PageData { get; private set; }  =
            "<!DOCTYPE>" +
            "<html>" +
            "  <head>" +
            "    <title>Test Page</title>" +
            "  </head>" +
            "  <body>" +
            "  </body>" +
            "</html>";

        /// <summary>
        /// When set, requests under /api/ are proxied to this URL instead of
        /// being answered with <see cref="PageData"/>.
        /// </summary>
        public string CloudUrl { get; private set; }

        /// <summary>
        /// Extra response headers added to the test-page response (the non-/api/
        /// branch). Proxied /api/ responses are not touched.
        /// </summary>
        public IReadOnlyDictionary<string, string> ExtraResponseHeaders { get; private set; }

        /// <summary>
        /// When set, every request is forwarded to this URL instead of serving a page.
        /// </summary>
        public string ProxyAllTo { get; private set; }

        /// <summary>
        /// Path prefixes proxied to another server, e.g. the example app
        /// under test.
        /// </summary>
        public IReadOnlyDictionary<string, string> ProxyRoutes { get; set; }

        /// <summary>
        /// Response headers forced onto responses proxied via
        /// <see cref="ProxyRoutes"/>.
        /// </summary>
        public IReadOnlyDictionary<string, string> ProxiedHeaderOverrides { get; set; }

        /// <summary>
        /// Method and path of each request handled since the last reset.
        /// Written by the listen loop and read from the test thread while the
        /// server is live, so it must tolerate concurrent access: enumerating
        /// a <see cref="ConcurrentQueue{T}"/> takes a snapshot rather than
        /// throwing when a request arrives mid-read.
        /// </summary>
        public ConcurrentQueue<string> RequestLog { get; } = new ConcurrentQueue<string>();

        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="listener">The HttpListener used to monitor for requests.</param>
        public HttpServer(HttpListener listener)
        {
            Listener = listener;
        }

        /// <summary>
        /// A <see cref="ProxyRoutes"/> key ending in '/' matches everything
        /// beneath it; any other key matches that one path exactly. The
        /// distinction matters because a prefix match on '/51Degrees.core.js'
        /// also captures '/51Degrees.core.json', which is a separate endpoint
        /// in the java web integration.
        /// </summary>
        private static bool RouteMatches(string routeKey, string path)
        {
            return routeKey.EndsWith("/", StringComparison.Ordinal)
                ? path.StartsWith(routeKey, StringComparison.OrdinalIgnoreCase)
                : string.Equals(path, routeKey, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reset the <see cref="RequestCount"/> counter to zero.
        /// </summary>
        public void ResetRequests()
        {
            RequestCount = 0;
            RequestLog.Clear();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="listener">The HttpListener used to monitor for requests.</param>
        /// <param name="pageData">The content returned by the HttpServer.</param>
        public HttpServer(HttpListener listener, string pageData)
        {
            Listener = listener;
            PageData = pageData;
        }

        /// <summary>
        /// Constructor for a proxying server: serves <paramref name="pageData"/>
        /// for non-/api/ paths, and forwards /api/ requests to <paramref name="cloudUrl"/>.
        /// Optional <paramref name="extraResponseHeaders"/> are added to the
        /// page response (not proxied /api/ responses).
        /// </summary>
        public HttpServer(
            HttpListener listener,
            string pageData,
            string cloudUrl,
            IReadOnlyDictionary<string, string> extraResponseHeaders = null)
        {
            Listener = listener;
            PageData = pageData;
            CloudUrl = cloudUrl;
            ExtraResponseHeaders = extraResponseHeaders;
        }

        /// <summary>
        /// Constructor for a reverse-proxy server that forwards every request
        /// to <paramref name="proxyAllTo"/>.
        /// </summary>
        public HttpServer(
            HttpListener listener,
            string proxyAllTo,
            IReadOnlyDictionary<string, string> extraResponseHeaders)
        {
            Listener = listener;
            ProxyAllTo = proxyAllTo;
            ExtraResponseHeaders = extraResponseHeaders;
        }

        /// <summary>
        /// Handle connections, either server the page data as content or
        /// shutdown the server if cancellation has been requested.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task HandleIncomingConnections(CancellationToken token)
        {
            bool runServer = true;

            while (runServer)
            {
                HttpListenerContext ctx = await Listener.GetContextAsync();

                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("Shutdown requested");
                    runServer = false;
                }

                RequestCount = RequestCount + 1;

                if (req.Url != null)
                {
                    RequestLog.Enqueue($"{req.HttpMethod} {req.Url.AbsolutePath}");
                }

                string proxyRouteTarget = null;
                if (ProxyRoutes != null && req.Url != null)
                {
                    foreach (var route in ProxyRoutes)
                    {
                        if (RouteMatches(route.Key, req.Url.AbsolutePath))
                        {
                            proxyRouteTarget = route.Value;
                            break;
                        }
                    }
                }

                try
                {
                    if (ProxyAllTo != null)
                    {
                        await ProxyTo(ProxyAllTo, req, resp, addExtraHeaders: true);
                    }
                    else if (proxyRouteTarget != null)
                    {
                        await ProxyTo(proxyRouteTarget, req, resp,
                            addExtraHeaders: false, ProxiedHeaderOverrides);
                    }
                    else if (CloudUrl != null
                        && req.Url != null
                        && req.Url.AbsolutePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                    {
                        await ProxyTo(CloudUrl, req, resp, addExtraHeaders: false);
                    }
                    else
                    {
                        byte[] data = Encoding.UTF8.GetBytes(PageData);
                        resp.ContentType = "text/html";
                        resp.ContentEncoding = Encoding.UTF8;
                        resp.ContentLength64 = data.LongLength;
                        if (ExtraResponseHeaders != null)
                        {
                            foreach (var kv in ExtraResponseHeaders)
                            {
                                resp.Headers.Add(kv.Key, kv.Value);
                            }
                        }
                        await resp.OutputStream.WriteAsync(data, 0, data.Length);
                    }
                }
                finally
                {
                    resp.Close();
                }
            }
        }

        private async Task ProxyTo(
            string baseUrl,
            HttpListenerRequest req,
            HttpListenerResponse resp,
            bool addExtraHeaders,
            IReadOnlyDictionary<string, string> headerOverrides = null)
        {
            var targetUri = new Uri(new Uri(baseUrl), req.Url.PathAndQuery.TrimStart('/'));
            using var outgoing = new HttpRequestMessage(new HttpMethod(req.HttpMethod), targetUri);

            // Forward the original Host so the bundle is built for the test-page
            // origin rather than the cloud's own host.
            if (!string.IsNullOrEmpty(req.UserHostName))
            {
                outgoing.Headers.Host = req.UserHostName;
            }

            foreach (string headerName in req.Headers.AllKeys)
            {
                if (headerName == null) continue;
                if (string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(headerName, "Keep-Alive", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(headerName, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;

                var values = req.Headers.GetValues(headerName);
                if (values == null) continue;
                outgoing.Headers.TryAddWithoutValidation(headerName, values);
            }

            if (req.HasEntityBody)
            {
                var ms = new MemoryStream();
                await req.InputStream.CopyToAsync(ms);
                ms.Position = 0;
                outgoing.Content = new StreamContent(ms);
                if (!string.IsNullOrEmpty(req.ContentType))
                {
                    outgoing.Content.Headers.TryAddWithoutValidation("Content-Type", req.ContentType);
                }
            }

            using var response = await _httpClient.SendAsync(
                outgoing, HttpCompletionOption.ResponseHeadersRead);
            resp.StatusCode = (int)response.StatusCode;
            if (response.ReasonPhrase != null)
            {
                resp.StatusDescription = response.ReasonPhrase;
            }

            CopyResponseHeaders(response.Headers, resp);
            CopyResponseHeaders(response.Content.Headers, resp);

            if (headerOverrides != null)
            {
                foreach (var kv in headerOverrides)
                {
                    resp.Headers[kv.Key] = kv.Value;
                }
            }

            if (addExtraHeaders && ExtraResponseHeaders != null)
            {
                foreach (var kv in ExtraResponseHeaders)
                {
                    resp.Headers[kv.Key] = kv.Value;
                }
            }

            using var bodyStream = await response.Content.ReadAsStreamAsync();
            await bodyStream.CopyToAsync(resp.OutputStream);
        }

        private static void CopyResponseHeaders(
            System.Net.Http.Headers.HttpHeaders source,
            HttpListenerResponse resp)
        {
            foreach (var header in source)
            {
                // Hop-by-hop / framing headers are managed by HttpListener itself.
                if (string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(header.Key, "Connection", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(header.Key, "Keep-Alive", StringComparison.OrdinalIgnoreCase)) continue;

                foreach (var value in header.Value)
                {
                    if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        resp.ContentType = value;
                    }
                    else
                    {
                        resp.Headers.Add(header.Key, value);
                    }
                }
            }
        }
    }
}
