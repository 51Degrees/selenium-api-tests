#nullable enable

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// Whether the regulation applies, and where the preference platform gets
/// that from.
/// <para>
/// **These cannot pass until the change that puts IsGdpr in the cloud is
/// released**, being pipeline-dotnet pull request 413 and then cloud pull
/// request 372. The harness entitlement record already asks for the
/// property, so the moment the container carries it these run. Until then
/// the first test reports inconclusive with that reason rather than
/// failing, because a red test nobody can fix teaches a reader to ignore
/// red tests.
/// </para>
/// <para>
/// The dialog is shown and an identifier created either way, because the
/// question the platform asks is the Model Terms usage, which is a matter
/// of contract, and not a consent under the regulation.
/// </para>
/// </summary>
[TestClass, TestCategory("Browser51Did")]
public class IsGdprAcceptanceTests : Browser51DidTestBase
{
    /// <summary>
    /// With the property carrying a value, the platform's framework
    /// surface reports it, and a false value does not stop the visitor
    /// being asked.
    /// </summary>
    [TestMethod]
    public void IsGdpr_ReadFromTheClientScript_SetsGdprApplies()
    {
        using var server = new PageServer();
        server.Put("/", Pages.Page(
            "Is the regulation in force",
            Pages.PlatformTag(new Pages.PlatformSettings()),
            Pages.ClientScriptTag()));

        using var visitor = NewVisitor(Chrome, server);
        visitor.Go(Harness.SiteA, "/");
        visitor.WaitForPlatform();
        visitor.WaitForClientRounds(1);

        var value = visitor.IsGdpr();
        if (value == "")
        {
            Assert.Inconclusive(
                "The client script's object carries no isgdpr value, so "
                + "there is nothing for the platform to read. The property "
                + "reaches the cloud in pipeline-dotnet pull request 413 "
                + "and then cloud pull request 372, and the harness "
                + "entitlement record already asks for it, so this runs as "
                + "soon as the container carries it.");
        }

        var applies = visitor.GdprApplies();
        Assert.AreEqual(
            string.Equals(value, "True", StringComparison.OrdinalIgnoreCase),
            applies,
            "the platform's framework surface must report what the client "
            + $"script resolved. The script said '{value}' and the surface "
            + $"said {applies}.");

        Assert.IsTrue(
            visitor.CardVisible("preferences"),
            "the visitor is asked whatever the answer is, because the "
            + "question is the Model Terms usage and that is contractual "
            + "rather than a consent under the regulation.");

        Assert.AreEqual(
            0,
            visitor.ConsoleMatching("isgdpr").Count(
                line => line.Contains(
                    "WARNING", StringComparison.OrdinalIgnoreCase)),
            "with a value to read there is nothing to warn about. The "
            + "console said: " + string.Join(" | ", visitor.Console()));
    }

    /// <summary>
    /// With no value to read, the platform says so once and carries on as
    /// though the regulation applies, which is the safe way round.
    /// </summary>
    [TestMethod]
    public void IsGdprAbsent_PlatformWarnsAndAssumesItApplies()
    {
        using var server = new PageServer();
        server.Put("/", Pages.Page(
            "No isgdpr",
            Pages.PlatformTag(new Pages.PlatformSettings()),
            Pages.ClientScriptTag()));

        using var visitor = NewVisitor(Chrome, server);
        visitor.Go(Harness.SiteA, "/");
        visitor.WaitForPlatform();
        visitor.WaitForClientRounds(1);

        if (visitor.IsGdpr() != "")
        {
            Assert.Inconclusive(
                "The harness resource key does carry a value for isgdpr, "
                + "so this is not the case under test. A key that does not "
                + "ask for the property is needed, which the harness does "
                + "not have a second of.");
        }

        Assert.IsTrue(
            visitor.GdprApplies(),
            "with nothing to read, the platform assumes the regulation "
            + "applies rather than assuming it does not.");
        Assert.IsTrue(
            visitor.ConsoleMatching("isgdpr").Count > 0,
            "the platform says once that it could not read the property, "
            + "because a publisher whose key does not carry it has no other "
            + "way of finding out. The console said: "
            + string.Join(" | ", visitor.Console()));
        Assert.IsTrue(
            visitor.CardVisible("preferences"),
            "the dialog is still shown and an identifier still created.");
    }
}
