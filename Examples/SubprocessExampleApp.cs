using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// Launches a language example as a child process, waits until it serves
    /// HTTP, and kills the whole process tree on dispose.
    /// </summary>
    public sealed class SubprocessExampleApp : IExampleApp
    {
        private readonly ExampleDescriptor _descriptor;
        private readonly StringBuilder _stdout = new();
        private readonly StringBuilder _stderr = new();
        private Process _process;
        private bool _started;

        /// <inheritdoc/>
        public Uri BaseUrl { get; private set; }

        /// <summary>Creates a launcher for the given language descriptor.</summary>
        public SubprocessExampleApp(ExampleDescriptor descriptor)
        {
            _descriptor = descriptor;
        }

        /// <summary>
        /// Returns the single file matching <paramref name="glob"/> under
        /// <paramref name="workingDir"/>.
        /// </summary>
        internal static string ResolveRunArtifact(string workingDir, string glob)
        {
            var combined = Path.Combine(workingDir, glob);
            var dir = Path.GetDirectoryName(combined);
            var pattern = Path.GetFileName(combined);
            if (dir == null || !Directory.Exists(dir))
            {
                throw new FileNotFoundException(
                    $"Run-artifact directory not found for glob '{glob}' under '{workingDir}'.");
            }
            var matches = Directory.GetFiles(dir, pattern);
            if (matches.Length != 1)
            {
                throw new FileNotFoundException(
                    $"Expected exactly one file matching '{glob}' under '{workingDir}', " +
                    $"found {matches.Length}: {string.Join(", ", matches)}");
            }
            return matches[0];
        }

        /// <inheritdoc/>
        public async Task StartAsync(ExampleAppOptions options, CancellationToken ct)
        {
            BaseUrl = new Uri($"http://localhost:{options.Port}/");

            var buildEnv = _descriptor.BuildEnv(options);

            if (_descriptor.BuildCommand != null)
            {
                await RunBuildAsync(buildEnv, options.ExtraEnv, ct);
            }

            var runArgs = new List<string>(_descriptor.Args);
            if (_descriptor.RunArtifactGlob != null)
            {
                runArgs.Add(ResolveRunArtifact(_descriptor.WorkingDir, _descriptor.RunArtifactGlob));
            }

            var psi = CreateProcessStartInfo(_descriptor.Command, runArgs, buildEnv, options.ExtraEnv);

            var appliedKeys = new List<string>(buildEnv.Keys);
            appliedKeys.AddRange(options.ExtraEnv.Keys);
            Console.WriteLine(
                $"[SubprocessExampleApp {_descriptor.Lang}] applied env keys: " +
                string.Join(", ", appliedKeys));

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) => { if (e.Data != null) { _stdout.AppendLine(e.Data); } };
            _process.ErrorDataReceived += (_, e) => { if (e.Data != null) { _stderr.AppendLine(e.Data); } };
            _process.Start();
            _started = true;
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            await WaitForReadyAsync(ct);
        }

        private ProcessStartInfo CreateProcessStartInfo(
            string fileName, IEnumerable<string> args,
            IDictionary<string, string> buildEnv, IReadOnlyDictionary<string, string> extraEnv)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = _descriptor.WorkingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            // the example uses its own config, so drop parent vars that would override its setup
            string[] stripPrefixes = { "PipelineOptions__", "ASPNETCORE_" };
            var keysToRemove = new List<string>();
            foreach (var kv in psi.Environment)
            {
                foreach (var prefix in stripPrefixes)
                {
                    if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        keysToRemove.Add(kv.Key);
                        break;
                    }
                }
            }
            foreach (var k in keysToRemove)
            {
                psi.Environment.Remove(k);
            }

            foreach (var kv in buildEnv)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
            foreach (var kv in extraEnv)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
            return psi;
        }

        private async Task RunBuildAsync(
            IDictionary<string, string> buildEnv,
            IReadOnlyDictionary<string, string> extraEnv,
            CancellationToken ct)
        {
            if (_descriptor.BuildTimeoutSeconds <= 0)
            {
                throw new InvalidOperationException(
                    $"{_descriptor.Lang} descriptor sets BuildCommand but BuildTimeoutSeconds is " +
                    $"{_descriptor.BuildTimeoutSeconds}; it must be greater than zero.");
            }

            var psi = CreateProcessStartInfo(
                _descriptor.BuildCommand,
                _descriptor.BuildArgs ?? Array.Empty<string>(),
                buildEnv, extraEnv);

            var output = new StringBuilder();
            using var build = new Process { StartInfo = psi, EnableRaisingEvents = true };
            build.OutputDataReceived += (_, e) => { if (e.Data != null) { output.AppendLine(e.Data); } };
            build.ErrorDataReceived += (_, e) => { if (e.Data != null) { output.AppendLine(e.Data); } };
            build.Start();
            build.BeginOutputReadLine();
            build.BeginErrorReadLine();

            try
            {
                await build.WaitForExitAsync(ct)
                    .WaitAsync(TimeSpan.FromSeconds(_descriptor.BuildTimeoutSeconds));
            }
            catch (TimeoutException)
            {
                try { build.Kill(entireProcessTree: true); } catch { /* best effort */ }
                throw new TimeoutException(
                    $"{_descriptor.Lang} example build ('{_descriptor.BuildCommand}') did not finish " +
                    $"within {_descriptor.BuildTimeoutSeconds}s.\nOUTPUT:\n{output}");
            }

            // make sure all output is captured before we read it
            build.WaitForExit();

            if (build.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{_descriptor.Lang} example build ('{_descriptor.BuildCommand}') failed " +
                    $"(exit code {build.ExitCode}).\nOUTPUT:\n{output}");
            }
        }

        private async Task WaitForReadyAsync(CancellationToken ct)
        {
            var readyUrl = new Uri(BaseUrl, _descriptor.ReadinessPath);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(_descriptor.StartupTimeoutSeconds);

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                if (_process.HasExited)
                {
                    throw new InvalidOperationException(
                        $"{_descriptor.Lang} example exited early (exit code {_process.ExitCode}).\n" +
                        $"STDOUT:\n{_stdout}\nSTDERR:\n{_stderr}");
                }

                try
                {
                    var resp = await http.GetAsync(readyUrl, ct);
                    if ((int)resp.StatusCode < 500)
                    {
                        return;
                    }
                }
                catch (HttpRequestException) { /* connection refused / not up yet */ }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested) { /* request timeout */ }

                await Task.Delay(500, ct);
            }

            throw new TimeoutException(
                $"{_descriptor.Lang} example did not become ready at {readyUrl} within " +
                $"{_descriptor.StartupTimeoutSeconds}s.\nSTDOUT:\n{_stdout}\nSTDERR:\n{_stderr}");
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_started && _process is { HasExited: false })
                {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                }
            }
            catch (TimeoutException)
            {
                // didn't exit in time, carry on
            }
            catch
            {
                // Best-effort teardown; ignore other shutdown errors.
            }
            _process?.Dispose();
        }
    }
}
