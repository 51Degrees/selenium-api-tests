#nullable enable

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FiftyOne.Did.Model;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// A page carrying PMP's tag and no client script tag
/// at all.
/// <para>
/// PMP has one route to the third party cookie result and to
/// whether the regulation applies, which is the client script's object.
/// Where the page has no client script tag PMP adds the script
/// itself, using the cloud that served it and the
/// resource key it already holds, and says in the console that it did. A
/// second route would be a second answer to the same question, and a
/// publisher who forgot the tag would get a dialog behaving differently
/// from the documented one with nothing to tell them why.
/// </para>
/// </summary>
[TestClass, TestCategory("Browser51Did")]
public class PmpAddsClientScriptTests : Browser51DidTestBase
{
    /// <summary>
    /// The whole of it in one page view, from the script arriving to the
    /// identifier coming back.
    /// </summary>
    [Browser51DidTest]
    public void NoClientScriptTag_PmpAddsItAndTheAnswerStillCreates()
    {
        RequireMarketingIdentifiers();
        using var visitor = NewVisitor(Chrome);
        visitor.Go(Harness.SiteA, Routes.PlatformOnly);
        visitor.WaitForPmp();

        // The script PMP added, named by where it came from and
        // which publisher it is for.
        Harness.Until(
            () => AddedClientScript(visitor) != null,
            "PMP added the client script. The console said: "
            + string.Join(" | ", visitor.Console()));
        var added = AddedClientScript(visitor)!;
        Assert.IsTrue(
            added.StartsWith(Harness.CloudUrl!, StringComparison.OrdinalIgnoreCase),
            "the script must come from the cloud that served PMP, "
            + "because that is the only cloud PMP knows about. It "
            + $"came from {added}.");
        Assert.IsTrue(
            added.Contains(Harness.Resource!, StringComparison.Ordinal),
            "the script must be asked for with the resource key PMP "
            + "already holds, or the cloud has no idea which "
            + $"publisher is asking. The URL was {added}.");

        // And it said so, which is the point of the convenience.
        Assert.IsTrue(
            visitor.ConsoleMatching(Harness.NoClientScriptMessage).Count > 0,
            "PMP must say in the console that there was no client "
            + "script object on the page, because a publisher who left the "
            + "tag out has no other way of finding out. The console said: "
            + string.Join(" | ", visitor.Console()));
        Assert.IsTrue(
            visitor.ConsoleMatching(Harness.AddingClientScriptMessage).Count > 0,
            "and that it is adding one, so the publisher knows where the "
            + "extra request came from. The console said: "
            + string.Join(" | ", visitor.Console()));

        // The object then exists, under the name in force.
        visitor.WaitForClientObject();
        Assert.IsTrue(
            visitor.HasTheNewClientScript(),
            "the script PMP added must be the new one, or PMP "
            + "has quietly given itself the old behaviour.");

        // The third party cookie result reached PMP through it,
        // which is the reason the script is added at all.
        Harness.Until(
            () => visitor.ThirdPartyCookies() != "",
            "the client script resolved the third party cookie result. The "
            + "console said: " + string.Join(" | ", visitor.Console()));

        // And the answer still creates, exactly as it would have on a page
        // that carried the tag.
        visitor.WaitForCard("preferences");
        visitor.Press("standard");
        Harness.Until(
            () => visitor.ClientRequests()
                .Any(r => r.Done && r.Form("id.usage") == "standard"),
            "the answer reached the cloud through the script PMP "
            + "added. The console said: "
            + string.Join(" | ", visitor.Console()));
        var answered = visitor.ClientRequests()
            .Last(r => r.Form("id.usage") == "standard");
        Assert.AreEqual(
            1,
            answered.FormCount("id.usage"),
            $"the answer must be sent once. The body was: {answered.Body}");
        foreach (var snippet in visitor.SnippetValueNames())
        {
            Assert.IsNotNull(
                answered.Form(snippet),
                "the request that creates the identifier must carry every "
                + $"snippet result. '{snippet}' was stored and not sent. "
                + $"The body was: {answered.Body}");
        }

        Harness.Until(
            () => visitor.Identifier() != "",
            "an identifier came back. The console said: "
            + string.Join(" | ", visitor.Console()));
        AssertDirect(
            visitor.Identifier(),
            "an answer given on PMP is stated directly");
        Assert.AreEqual(
            Usage.Standard,
            UsageOf(visitor.Identifier(), "the answer"),
            "the visitor pressed standard.");

        // One script, not two. PMP adds one only where there is
        // none, so nothing here may trip the warning about a second copy.
        Assert.AreEqual(
            0,
            visitor.ConsoleMatching(Harness.SecondInstanceWarning).Count,
            "PMP added a second copy of the client script, or "
            + "added one to a page that already had it. The console said: "
            + string.Join(" | ", visitor.Console()));
        Assert.AreEqual(
            1,
            ClientScriptsOnThePage(visitor),
            "there must be exactly one client script on the page. Its "
            + "sources were: " + string.Join(", ", visitor.ScriptSources()));
    }

    /// <summary>
    /// The publisher may name the object something other than the default,
    /// and PMP then asks for the script under that name, finds it
    /// under that name, and says which name it used.
    /// </summary>
    [Browser51DidTest]
    public void ObjectNameAttribute_NamesTheObjectEverywhere()
    {
        const string objectName = Harness.NamedObject;
        using var visitor = NewVisitor(Chrome);
        visitor.Go(Harness.SiteA, Routes.NamedObject);
        visitor.WaitForPmp();

        Harness.Until(
            () => AddedClientScript(visitor) != null,
            "PMP added the client script. The console said: "
            + string.Join(" | ", visitor.Console()));
        var added = AddedClientScript(visitor)!;
        Assert.IsTrue(
            added.Contains(objectName, StringComparison.Ordinal),
            "the name the publisher asked for must be on the URL, or the "
            + "cloud renders the script under the default name and PMP "
            + $"then looks for the wrong object. The URL was {added}.");

        visitor.WaitForClientObject(objectName);
        Assert.IsTrue(
            visitor.HasTheNewClientScript(objectName),
            $"the object under '{objectName}' must be the client script's.");
        Assert.IsFalse(
            visitor.HasClientObject(Harness.DefaultObjectName),
            "nothing may be put under the default name when the publisher "
            + "asked for another one, because two objects would be two "
            + "instances and the page would warn about the second.");

        var named = visitor.ConsoleMatching(objectName);
        Assert.IsTrue(
            named.Count > 0,
            "every message about the object names whichever name is in "
            + "force, so a publisher reading the console can tell which "
            + "object is being talked about. The console said: "
            + string.Join(" | ", visitor.Console()));
    }

    /// <summary>
    /// The attribute left off uses the default name, which is what the
    /// documentation says and what an existing page relies on.
    /// </summary>
    [Browser51DidTest]
    public void ObjectNameAttributeAbsent_UsesTheDefaultName()
    {
        using var visitor = NewVisitor(Chrome);
        visitor.Go(Harness.SiteA, Routes.PlatformOnly);
        visitor.WaitForPmp();
        visitor.WaitForClientObject(Harness.DefaultObjectName);
        Assert.IsTrue(
            visitor.HasTheNewClientScript(Harness.DefaultObjectName),
            "with no name asked for, the object is the default one.");
    }

    /// <summary>
    /// The client script PMP added, or null where it has not
    /// added one yet. Anything the cloud serves as a resource key script
    /// counts, and the page carried none of its own, so whatever is there
    /// was added.
    /// </summary>
    private static string? AddedClientScript(Visitor visitor)
        => visitor.ScriptSources()
            .FirstOrDefault(src =>
                src.Contains("/api/v4/", StringComparison.OrdinalIgnoreCase)
                && src.Contains(".js", StringComparison.OrdinalIgnoreCase)
                && src.Contains("/pmp", StringComparison.OrdinalIgnoreCase)
                    == false);

    private static int ClientScriptsOnThePage(Visitor visitor)
        => visitor.ScriptSources()
            .Count(src =>
                src.Contains("/api/v4/", StringComparison.OrdinalIgnoreCase)
                && src.Contains(".js", StringComparison.OrdinalIgnoreCase)
                && src.Contains("/pmp", StringComparison.OrdinalIgnoreCase)
                    == false);
}
