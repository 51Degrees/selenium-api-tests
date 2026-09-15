#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// The publisher's website, for the length of one test. It serves the page
/// fixtures and nothing else.
/// <para>
/// It is deliberately not a proxy for the cloud. The pages load the client
/// script and the preference platform straight from the cloud's own origin,
/// which is what makes the cloud a third party to the page and the shared
/// cookie a third party cookie. A proxy would put both on one origin and
/// the first demonstration would pass for the wrong reason. What a test
/// needs to see of the traffic is recorded inside the browser instead, by
/// the recorder in <see cref="Pages"/>, which reads the request bodies and
/// the responses the page actually sent and received.
/// </para>
/// <para>
/// It answers on a plain socket and pays no attention to the host name in
/// the request, rather than using the framework's own listener, because
/// that one routes by the name and registering any name but localhost
/// needs an administrator on Windows. A developer would then get a
/// different run from CI, which is the thing most likely to let a fault
/// through. Here every publisher site name reaches the same server, and
/// the browsers are told to resolve those names to this machine, so the
/// only thing separating one site from another is the name in the
/// request, which is exactly what a browser uses to decide what is third
/// party.
/// </para>
/// </summary>
public sealed class PageServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<string, string> _pages = new(
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Method, host and path of every request served, newest last. A test
    /// reads it while the server is live, so it is a queue that takes a
    /// snapshot when enumerated rather than one that throws.
    /// </summary>
    public ConcurrentQueue<string> RequestLog { get; } = new();

    /// <summary>The port the sites are served on.</summary>
    public int Port { get; }

    public PageServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = Task.Run(Accept);
    }

    /// <summary>
    /// Puts a page at a path. The same path may be replaced between
    /// navigations, which is how a test changes what the second page view
    /// carries.
    /// </summary>
    public void Put(string path, string html)
        => _pages[Normalise(path)] = html;

    /// <summary>
    /// The address of a page on one of the publisher sites. The cache
    /// buster is on every navigation, because a page held in the browser's
    /// cache would run the recorder from an earlier test.
    /// </summary>
    public string UrlFor(string site, string path)
        => $"http://{site}:{Port}{Normalise(path)}"
            + $"?v={DateTime.UtcNow.Ticks}";

    /// <summary>Every request served since the last clear.</summary>
    public IReadOnlyList<string> Requests => new List<string>(RequestLog);

    public void ClearRequests() => RequestLog.Clear();

    private async Task Accept()
    {
        while (_stopping.IsCancellationRequested == false)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(
                    _stopping.Token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The listener was stopped, which is how a test ends.
                return;
            }
            _ = Task.Run(() => Serve(client));
        }
    }

    private async Task Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(
                    stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync()
                    .ConfigureAwait(false);
                if (string.IsNullOrEmpty(requestLine))
                {
                    return;
                }
                var host = string.Empty;
                string? header;
                // Headers are read to the blank line so the browser's
                // request is fully consumed, and the host is kept because
                // the log is the one place a test can see which site a
                // request was for.
                while (string.IsNullOrEmpty(
                    header = await reader.ReadLineAsync().ConfigureAwait(false))
                    == false)
                {
                    if (header!.StartsWith(
                        "Host:", StringComparison.OrdinalIgnoreCase))
                    {
                        host = header.Substring(5).Trim();
                    }
                }
                var parts = requestLine.Split(' ');
                var method = parts.Length > 0 ? parts[0] : "GET";
                var path = Normalise(parts.Length > 1 ? parts[1] : "/");
                RequestLog.Enqueue($"{method} {host}{path}");

                var found = _pages.TryGetValue(path, out var html);
                var body = Encoding.UTF8.GetBytes(found ? html! : "not here");
                var head = new StringBuilder()
                    .Append(found ? "HTTP/1.1 200 OK\r\n"
                        : "HTTP/1.1 404 Not Found\r\n")
                    .Append("Content-Type: text/html; charset=utf-8\r\n")
                    .Append("Content-Length: ")
                    .Append(body.Length)
                    .Append("\r\n")
                    // Nothing here may be reused between page views, or a
                    // test that navigates twice measures the first page
                    // view twice.
                    .Append("Cache-Control: no-store, no-cache, must-revalidate\r\n")
                    .Append("Connection: close\r\n\r\n")
                    .ToString();
                var headBytes = Encoding.ASCII.GetBytes(head);
                await stream.WriteAsync(headBytes).ConfigureAwait(false);
                await stream.WriteAsync(body).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A browser that went away mid response is not a failure of
                // the thing under test, and the assertions are on what the
                // page recorded rather than on what this served.
            }
        }
    }

    private static string Normalise(string path)
    {
        var trimmed = path.Split('?')[0].Split('#')[0];
        return trimmed.StartsWith("/", StringComparison.Ordinal)
            ? trimmed
            : "/" + trimmed;
    }

    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
            // Already stopped.
        }
        _stopping.Dispose();
    }
}
