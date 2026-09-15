#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FiftyOne.Did.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// A test method whose every result has the resource key taken out of it
/// before the runner sees it, being the failure message, the stack trace
/// and everything the test wrote.
/// <para>
/// This suite is public and its CI log is public, and a failure message
/// here routinely quotes what a page did, which includes the client
/// script's address, and that address names the resource key. Redacting
/// each message by hand leaves the next message written to leak it, so it
/// is done once, here, for every result. <see cref="Browser51DidTestBase"/>
/// refuses to run a test that is not marked with this attribute.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class Browser51DidTestAttribute : TestMethodAttribute
{
    /// <summary>Passes the declaring file and line on, as MSTest needs.</summary>
    public Browser51DidTestAttribute(
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = -1)
        : base(callerFilePath, callerLineNumber)
    {
    }

    /// <inheritdoc/>
    public override async Task<TestResult[]> ExecuteAsync(
        ITestMethod testMethod)
    {
        var results = await base.ExecuteAsync(testMethod)
            .ConfigureAwait(false);
        foreach (var result in results)
        {
            Redact(result);
        }
        return results;
    }

    private static void Redact(TestResult result)
    {
        result.LogOutput = RedactedOrNull(result.LogOutput);
        result.LogError = RedactedOrNull(result.LogError);
        result.DebugTrace = RedactedOrNull(result.DebugTrace);
        result.TestContextMessages = RedactedOrNull(result.TestContextMessages);
        var failure = result.TestFailureException;
        if (failure is null || string.IsNullOrEmpty(Harness.Resource))
        {
            return;
        }
        var whole = failure.ToString();
        if (whole.Contains(Harness.Resource, StringComparison.Ordinal) == false)
        {
            return;
        }
        // The exception cannot be edited, so a new one carries the redacted
        // text. Its own stack trace would point here, so the original one is
        // kept in the message, where the line that failed can still be read.
        var text = Harness.Redacted(
            $"{InnermostMessage(failure)}\nWhere it failed, kept because "
            + $"the message was redacted:\n{whole}");
        result.TestFailureException =
            result.Outcome == UnitTestOutcome.Inconclusive
                ? new AssertInconclusiveException(text)
                : new AssertFailedException(text);
    }

    private static string InnermostMessage(Exception failure)
    {
        var inner = failure;
        while (inner.InnerException is not null)
        {
            inner = inner.InnerException;
        }
        return inner.Message;
    }

    private static string? RedactedOrNull(string? text)
        => text is null ? null : Harness.Redacted(text);
}

/// <summary>
/// What every browser acceptance test class shares, being the guard, the
/// skip when the harness is not configured, the demo, and the reading of a
/// 51Did.
/// <para>
/// The guard runs once for each class from the set up, not from inside a
/// test, so that a cloud built against the old client script fails every
/// test in the class rather than letting one of them pass for the wrong
/// reason.
/// </para>
/// </summary>
public abstract class Browser51DidTestBase
{
    /// <summary>
    /// The browsers these tests are written for. Both are run where the
    /// behaviour under test differs between them, which is anything the
    /// shared choice travels on.
    /// </summary>
    protected const string Chrome = "Chrome";

    /// <summary>The second browser, for the same reason.</summary>
    protected const string Firefox = "Firefox";

    /// <summary>Set by MSTest, and used to find the running test.</summary>
    public TestContext TestContext { get; set; } = null!;

    /// <summary>
    /// Fails every test in the class unless the client script the demo's
    /// pages load is the new one. See
    /// <see cref="Harness.RequireTheNewClientScript"/>.
    /// </summary>
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void GuardTheClientScript(TestContext context)
    {
        if (Harness.Configured == false)
        {
            // Nothing to guard. Each test reports inconclusive below and
            // says which variables are missing.
            return;
        }
        Harness.RequireTheNewClientScript();
    }

    [TestInitialize]
    public void RequireHarness()
    {
        var method = GetType().GetMethod(TestContext.TestName ?? string.Empty);
        if (method?.GetCustomAttribute<Browser51DidTestAttribute>() is null)
        {
            Assert.Fail(
                $"{TestContext.TestName} is not marked [Browser51DidTest], "
                + "so a failure in it could print the resource key into a "
                + "public log. Mark it [Browser51DidTest] instead of "
                + "[TestMethod].");
        }
        if (Harness.Configured == false)
        {
            Assert.Inconclusive(Harness.NotConfiguredReason);
        }
        Demo.Chosen.EnsureStarted();
    }

    /// <summary>
    /// A browser, which will load the demo's pages as the two publisher
    /// sites.
    /// </summary>
    protected static Visitor NewVisitor(string browser)
        => new(
            browser == Firefox ? Harness.NewFirefox() : Harness.NewChrome(),
            browser);

    /// <summary>
    /// Refuses to go on where the cloud will not hold a choice for this
    /// resource key, because the test would then be failing on the harness
    /// rather than on the code. The service is asked rather than a flag
    /// being read, so the reason reported is the service's own.
    /// </summary>
    protected static void RequireSharing() => Harness.RequireSharedStore();

    /// <summary>
    /// Refuses to go on where this resource key cannot create an
    /// identifier for a standard or personalized answer, which needs a
    /// resource key whose products include CloudV5FODiD. The service is
    /// asked, and the skip carries its own reason.
    /// </summary>
    protected static void RequireMarketingIdentifiers()
        => Harness.RequireMarketingIdentifiers();

    #region Reading a 51Did

    /// <summary>
    /// Parses an identifier the page is holding, failing with what was
    /// wrong when it is not one.
    /// </summary>
    protected static FodId Parse(string identifier, string what)
    {
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(identifier),
            $"{what}: the page holds no identifier at all.");
        Assert.IsTrue(
            FodId.TryParse(identifier, out var parsed, out var status),
            $"{what}: '{identifier}' is not a 51Did ({status}).");
        Assert.IsNotNull(
            parsed,
            $"{what}: '{identifier}' parsed to nothing ({status}).");
        return parsed!;
    }

    /// <summary>
    /// The identifier says the usage was stated by the caller, which is
    /// what the preference platform does, so the signal source is direct.
    /// </summary>
    protected static void AssertDirect(string identifier, string what)
    {
        var parsed = Parse(identifier, what);
        Assert.IsFalse(
            parsed.UsageFromConsent,
            $"{what}: the usage should have been stated directly, so the "
            + "flag saying it was decoded from a framework string must be "
            + $"clear. The identifier reports usage {parsed.Usage}.");
    }

    /// <summary>
    /// The identifier says the usage was decoded from a framework string,
    /// which is what a consent platform page produces.
    /// </summary>
    protected static void AssertFromConsentString(
        string identifier, string what, Usage expected)
    {
        var parsed = Parse(identifier, what);
        Assert.IsTrue(
            parsed.UsageFromConsent,
            $"{what}: the usage came from a framework string, so the flag "
            + "recording that must be set. The identifier reports usage "
            + $"{parsed.Usage}.");
        Assert.AreEqual(
            expected,
            parsed.Usage,
            $"{what}: the framework string granted {expected}.");
    }

    /// <summary>The usage an identifier carries.</summary>
    protected static Usage UsageOf(string identifier, string what)
        => Parse(identifier, what).Usage;

    #endregion
}
