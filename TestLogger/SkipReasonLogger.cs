using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace FiftyOne.SeleniumTests.TestLogger
{
    /// <summary>
    /// Prints the reason for every skipped test to the console.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The console logger that <c>dotnet test</c> uses prints the name of a
    /// skipped test and nothing else, so a run that skips everything reads as
    /// a success and the reason is only visible by re-running with
    /// <c>--logger "console;verbosity=detailed"</c>. A test that writes the
    /// reason itself does not help, as output written inside a test goes to
    /// the test host and never reaches the console.
    /// </para>
    /// <para>
    /// A logger runs in the same process as the console output, so it can
    /// print. This one is discovered because the assembly name ends with
    /// ".TestLogger" and it is built next to the tests, and it is turned on by
    /// test.runsettings, which the test project points at. Nothing has to be
    /// added to the <c>dotnet test</c> command line.
    /// </para>
    /// </remarks>
    [FriendlyName(FriendlyNameValue)]
    [ExtensionUri(ExtensionUriValue)]
    public class SkipReasonLogger : ITestLogger
    {
        /// <summary>Name used to enable this logger.</summary>
        public const string FriendlyNameValue = "skipreasons";

        /// <summary>Unique identifier of this logger.</summary>
        public const string ExtensionUriValue =
            "logger://51degrees/selenium/skipreasons/v1";

        // Noise the assertion and the test framework add around the reason.
        private static readonly string[] _prefixesToTrim =
        {
            "Assert.Inconclusive failed. ",
            "Microsoft.VisualStudio.TestTools.UnitTesting."
                + "AssertInconclusiveException: ",
        };

        /// <summary>Subscribes to the results of the run.</summary>
        public void Initialize(TestLoggerEvents events, string testRunDirectory)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }
            events.TestResult += (sender, e) =>
            {
                var line = Format(e?.Result);
                if (line != null)
                {
                    Console.WriteLine(line);
                }
            };
        }

        /// <summary>
        /// The line to print for a result, or null when the result is not a
        /// skip or carries no reason.
        /// </summary>
        public static string Format(TestResult result)
        {
            if (result == null
                || result.Outcome != TestOutcome.Skipped
                || string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return null;
            }
            return $"  Skipped {result.TestCase?.DisplayName}, because: "
                + Tidy(result.ErrorMessage);
        }

        /// <summary>
        /// Removes the wrapping the assertion adds, so the reason the test
        /// gave is what the reader sees. Line breaks become spaces, keeping
        /// one skip to one line.
        /// </summary>
        public static string Tidy(string message)
        {
            if (message == null)
            {
                return null;
            }
            var tidied = message.Replace("\r\n", " ").Replace('\n', ' ').Trim();
            bool trimmed;
            do
            {
                trimmed = false;
                foreach (var prefix in _prefixesToTrim)
                {
                    var at = tidied.IndexOf(prefix, StringComparison.Ordinal);
                    if (at >= 0)
                    {
                        tidied = tidied.Remove(at, prefix.Length);
                        trimmed = true;
                    }
                }
            }
            while (trimmed);
            return tidied;
        }
    }
}
