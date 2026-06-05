using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// How to launch one language's example as a subprocess and map the
    /// canonical options onto that example's own configuration mechanism.
    /// </summary>
    /// <param name="Lang">Language identifier (e.g. "dotnet").</param>
    /// <param name="WorkingDir">Directory to launch the process from.</param>
    /// <param name="Command">Executable to run.</param>
    /// <param name="Args">Arguments for the executable.</param>
    /// <param name="ReadinessPath">Path polled for HTTP readiness.</param>
    /// <param name="StartupTimeoutSeconds">Seconds to wait for readiness after launch.</param>
    /// <param name="BuildEnv">Maps options to environment variables for the process.</param>
    /// <param name="BuildCommand">Optional build executable run once before launch (null = no build phase).</param>
    /// <param name="BuildArgs">Arguments for the build executable.</param>
    /// <param name="RunArtifactGlob">Optional glob, relative to WorkingDir, resolved after the build and appended as the final run argument.</param>
    /// <param name="BuildTimeoutSeconds">Seconds to wait for the build to finish.</param>
    public sealed record ExampleDescriptor(
        string Lang,
        string WorkingDir,
        string Command,
        IReadOnlyList<string> Args,
        string ReadinessPath,
        int StartupTimeoutSeconds,
        Func<ExampleAppOptions, IDictionary<string, string>> BuildEnv,
        string BuildCommand = null,
        IReadOnlyList<string> BuildArgs = null,
        string RunArtifactGlob = null,
        int BuildTimeoutSeconds = 0);
}
