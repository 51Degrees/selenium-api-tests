#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FiftyOne.Did.Model;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// What every browser acceptance test class shares, being the guard, the
/// skip when the harness is not configured, and the reading of a 51Did.
/// <para>
/// The guard runs once for each class from the set up, not from inside a
/// test, so that a container built against the old client script fails
/// every test in the class rather than letting one of them pass for the
/// wrong reason.
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

    /// <summary>
    /// Fails every test in the class unless the client script served by
    /// the endpoint under test is the new one. See
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
        if (Harness.Configured == false)
        {
            Assert.Inconclusive(
                "The browser acceptance harness is not configured. Set "
                + "FIFTYONE_CONTEXT_SELENIUM_BASEURL and "
                + "FIFTYONE_CONTEXT_SELENIUM_RESOURCE, or run ci/test.ps1 "
                + "with -Browser51Did $true, which starts the container and "
                + "sets them. See README.md.");
        }
    }

    /// <summary>
    /// A browser looking at a website served for the length of this test.
    /// </summary>
    protected static Visitor NewVisitor(
        string browser,
        PageServer server,
        IReadOnlyDictionary<string, object>? preferences = null)
        => new(
            browser == Firefox
                ? Harness.NewFirefox(preferences)
                : Harness.NewChrome(preferences),
            server,
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
    /// licence carrying the CloudV5FODiD product. See
    /// <see cref="Harness.Resource"/> for why a throwaway record cannot
    /// have one.
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
