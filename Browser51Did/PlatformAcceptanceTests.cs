#nullable enable

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FiftyOne.Did.Model;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// The preference platform and the client script on one page, which is the
/// arrangement the design is built around, proved in a real browser.
/// <para>
/// These are demonstrations 1, 3 and 4 of the acceptance list, with the
/// change of answer and the alternative answer that came from the later
/// decisions. Every assertion is on an ordering of requests and page
/// states, never on a clock.
/// </para>
/// </summary>
[TestClass, TestCategory("Browser51Did")]
public class PlatformAcceptanceTests : Browser51DidTestBase
{
    /// <summary>
    /// Demonstration 3, and the one that carries the most. A first visit,
    /// the visitor answers the first card, the answer reaches the client
    /// script, the full sequence runs with the usage known and an
    /// identifier comes back with the signal source recorded as direct.
    /// The second card is up before any of that finished, because nothing
    /// waits between the two cards.
    /// </summary>
    [TestMethod]
    public void CommonPath_PlatformThenScript_Chrome()
        => CommonPath(Chrome, platformFirst: true);

    /// <summary>
    /// The same, in the other browser. Demonstration 9 asks for both,
    /// because anything the shared choice travels on behaves differently
    /// between them.
    /// </summary>
    [TestMethod]
    public void CommonPath_PlatformThenScript_Firefox()
        => CommonPath(Firefox, platformFirst: true);

    /// <summary>
    /// The same page with the two tags the other way round. The platform's
    /// bundle loads on its own timetable, so the announcement has to reach
    /// the client script whichever tag the publisher wrote first, and a
    /// design that only worked one way round would pass the test above and
    /// fail on half the customers' pages.
    /// </summary>
    [TestMethod]
    public void CommonPath_ScriptThenPlatform_Chrome()
        => CommonPath(Chrome, platformFirst: false);

    private void CommonPath(string browser, bool platformFirst)
    {
        using var server = new PageServer();
        RequireMarketingIdentifiers();
        var settings = new Pages.PlatformSettings();
        var platform = Pages.PlatformTag(settings);
        var script = Pages.ClientScriptTag();
        server.Put("/", Pages.Page(
            "Common path",
            platformFirst ? platform : script,
            platformFirst ? script : platform));

        using var visitor = NewVisitor(browser, server);
        visitor.Go(Harness.SiteA, "/");

        // The first round. Nobody has been asked yet, so nothing may be
        // created, which is the rule the whole programme exists for.
        visitor.WaitForClientRounds(1);
        Assert.IsTrue(
            visitor.HasTheNewClientScript(),
            "the page loaded a client script with no user prompt block in "
            + "it, so this test would be proving the old behaviour. The "
            + "guard passed, so the container served the new script and "
            + "something else on the page loaded an old one.");
        var first = visitor.ClientRequests()[0];
        Assert.IsNull(
            first.Form("id.usage"),
            "the first request went before anyone was asked, so it must "
            + $"carry no answer. It carried: {first.Body}");
        Assert.AreEqual(
            "",
            visitor.Identifier(),
            "no answer means no identifier, however much else the cloud "
            + "resolved.");

        // The visitor answers.
        visitor.WaitForPlatform();
        visitor.WaitForCard("preferences");
        var roundsBefore = visitor.ClientRequests().Count;
        // How many rounds had already finished, which is what the share
        // card must appear before any more of. The page may well have
        // finished more than one by now, because the snippets it was asked
        // to run produce a round of their own, so this is read rather than
        // assumed to be one.
        var doneBefore = visitor.ClientRequests().Count(r => r.Done);
        visitor.Press("standard");

        // The second card is up before the refresh has finished. Both
        // readings come from one call, so there is no gap between them in
        // which the answer could change.
        (bool CardVisible, int RoundsDone) atShare = (false, -1);
        Harness.Until(
            () =>
            {
                var seen = visitor.Observe("share");
                if (seen.CardVisible && atShare.RoundsDone < 0)
                {
                    atShare = seen;
                }
                return atShare.RoundsDone >= 0;
            },
            $"the share card appeared in {browser}. The console said: "
            + $"{string.Join(" | ", visitor.Console())}");
        Assert.AreEqual(
            doneBefore,
            atShare.RoundsDone,
            "the share card must follow the first card at once, with "
            + "nothing waiting on the refresh. When it appeared the client "
            + $"script had finished {atShare.RoundsDone} rounds rather than "
            + $"the {doneBefore} it had finished at the click, so something "
            + "waited.");

        // Exactly one further request, carrying the answer once and every
        // snippet result the page had worked out.
        visitor.WaitForClientRounds(roundsBefore + 1);
        var requests = visitor.ClientRequests();
        Assert.AreEqual(
            roundsBefore + 1,
            requests.Count,
            "answering the first card must produce exactly one further "
            + "request. The scripts on the page were: "
            + string.Join(", ", visitor.ScriptSources())
            + ". The console said: "
            + string.Join(" | ", visitor.Console())
            + ". The requests were: "
            + string.Join(" || ", requests.Select(r => r.ToString())));
        var answered = requests[requests.Count - 1];
        Assert.AreEqual(
            "standard",
            answered.Form("id.usage"),
            $"the answer must reach the cloud. The body was: {answered.Body}");
        Assert.AreEqual(
            1,
            answered.FormCount("id.usage"),
            "the answer must be sent once. The server takes the first value "
            + "of a repeated key and warns, so sending it twice would "
            + $"create nothing. The body was: {answered.Body}");
        foreach (var snippet in visitor.SnippetValueNames())
        {
            Assert.IsNotNull(
                answered.Form(snippet),
                $"the request that creates the identifier must carry every "
                + $"snippet result. '{snippet}' was stored by the page and "
                + $"not sent. The body was: {answered.Body}");
        }

        // The identifier, and the flag that says the answer was stated
        // rather than decoded from a framework string.
        var identifier = visitor.Identifier();
        AssertDirect(
            identifier,
            "an answer given on the platform is stated directly");
        Assert.AreEqual(
            Usage.Standard,
            UsageOf(identifier, "the answer was standard"),
            "the visitor pressed standard.");
        Assert.IsTrue(
            answered.Response.Contains("fodid", StringComparison.Ordinal),
            "the response to the answered request must carry the identifier "
            + $"section. It was: {Truncate(answered.Response)}");

        // Nothing loaded a second copy of the client script.
        Assert.AreEqual(
            0,
            visitor.ConsoleMatching(Harness.SecondInstanceWarning).Count,
            "a second instance of the client script was loaded onto the "
            + "page. The console said: "
            + string.Join(" | ", visitor.Console()));

        // And the shared write, which is the second card's whole purpose.
        RequireSharing();
        visitor.Press("share-accept");
        Harness.Until(
            () => visitor.SharedStoreRequests()
                .Any(r => r.Method == "POST" && r.Done),
            $"the shared choice was written in {browser}. The console said: "
            + $"{string.Join(" | ", visitor.Console())}");
        var write = visitor.SharedStoreRequests()
            .Last(r => r.Method == "POST");
        Assert.AreEqual(
            200,
            write.Status,
            "the cloud must accept the shared write. It answered "
            + $"{write.Status}: {Truncate(write.Response)}");
    }

    /// <summary>
    /// Demonstration 1. A choice made on one site is read on another, the
    /// visitor is not asked again, and the client script still creates an
    /// identifier from the answer with the signal source recorded as
    /// direct.
    /// </summary>
    [TestMethod]
    public void SharedChoice_SecondSite_NoDialogAndDirectFlag_Chrome()
        => SharedChoice(Chrome);

    /// <summary>
    /// The same across browsers, because the shared choice travels on a
    /// third party cookie and that is the thing the two browsers treat
    /// differently.
    /// </summary>
    [TestMethod]
    public void SharedChoice_SecondSite_NoDialogAndDirectFlag_Firefox()
        => SharedChoice(Firefox);

    private void SharedChoice(string browser)
    {
        using var server = new PageServer();
        RequireMarketingIdentifiers();
        RequireSharing();
        var page = Pages.Page(
            "Shared choice",
            Pages.PlatformTag(new Pages.PlatformSettings()),
            Pages.ClientScriptTag());
        server.Put("/", page);

        using var visitor = NewVisitor(browser, server);

        // Site A, where the visitor answers and agrees to share.
        visitor.Go(Harness.SiteA, "/");
        visitor.WaitForCard("preferences");
        visitor.Press("standard");
        visitor.WaitForCard("share");
        visitor.Press("share-accept");
        Harness.Until(
            () => visitor.SharedStoreRequests()
                .Any(r => r.Method == "POST" && r.Done && r.Status == 200),
            $"the choice was shared from {Harness.SiteA} in {browser}. The "
            + $"console said: {string.Join(" | ", visitor.Console())}");

        // Site B, a first visit, in the same browser.
        visitor.Go(Harness.SiteB, "/");
        visitor.WaitForPlatform();
        visitor.WaitForClientRounds(1);

        var read = visitor.SharedStoreRequests()
            .FirstOrDefault(r => r.Method == "GET" && r.Done);
        Assert.IsNotNull(
            read,
            "the platform must ask the shared store what this visitor "
            + "already chose. It made no such call. The requests were: "
            + string.Join(" || ",
                visitor.Requests().Select(r => r.ToString())));
        Assert.IsTrue(
            read!.Response.Contains("standard", StringComparison.Ordinal),
            "the shared store must answer with the choice made on the other "
            + $"site. It answered: {Truncate(read.Response)}");

        Assert.IsTrue(
            visitor.BubbleOnly(),
            "a visitor who has already answered is not asked again, so only "
            + $"the floating button shows. The dialog is: {visitor.PlatformState()}"
            + ". The console said: "
            + string.Join(" | ", visitor.Console()));

        Harness.Until(
            () => visitor.ClientRequests()
                .Any(r => r.Done && r.Form("id.usage") == "standard"),
            "the answer read from the shared store reached the client "
            + $"script in {browser}. The console said: "
            + $"{string.Join(" | ", visitor.Console())}");
        var answered = visitor.ClientRequests()
            .Last(r => r.Form("id.usage") == "standard");
        Assert.AreEqual(
            1,
            answered.FormCount("id.usage"),
            "the answer must be sent once, or the server drops the repeat "
            + $"and creates nothing. The body was: {answered.Body}");

        var identifier = visitor.Identifier();
        AssertDirect(
            identifier,
            "a choice made on the platform stays a stated usage on the "
            + "second site");
        Assert.AreEqual(
            Usage.Standard,
            UsageOf(identifier, "the choice was standard"),
            "the choice carried across is the one that was made.");

        Assert.AreEqual(
            "standard",
            visitor.PlatformPreference(),
            "the platform's own getter must answer with the choice it is "
            + "acting on, on the second site as much as on the first.");

        var stored = visitor.LocalStorageKeys();
        Assert.AreEqual(
            0,
            stored.Count,
            "nothing new is written to browser storage by the platform, and "
            + "the second site's answer lives in the shared store rather "
            + "than being copied here. It wrote: "
            + string.Join(", ", stored));
    }

    /// <summary>
    /// A change of answer on the same page. The sequence goes up, a
    /// different identifier comes back, and page code that registered a
    /// change handler is told.
    /// </summary>
    [TestMethod]
    public void ChangeOfAnswer_SamePage_NewIdentifierAndOnChange()
    {
        using var server = new PageServer();
        RequireMarketingIdentifiers();
        server.Put("/", Pages.Page(
            "Change of answer",
            Pages.PlatformTag(new Pages.PlatformSettings()),
            Pages.ClientScriptTag(),
            Pages.ChangeWatcher(Pages.DefaultObjectName)));

        using var visitor = NewVisitor(Chrome, server);
        visitor.Go(Harness.SiteA, "/");
        visitor.WaitForCard("preferences");
        visitor.Press("standard");
        Harness.Until(
            () => visitor.Identifier() != "",
            "the first answer produced an identifier. The console said: "
            + string.Join(" | ", visitor.Console()));
        var firstIdentifier = visitor.Identifier();
        var roundsBefore = visitor.ClientRequests().Count;
        var sequenceBefore = Sequence(visitor.ClientRequests().Last());

        // The visitor changes their mind, through the platform.
        visitor.OpenPlatform();
        visitor.WaitForCard("preferences");
        visitor.Press("personalized");

        Harness.Until(
            () => visitor.Identifier() != ""
                && visitor.Identifier() != firstIdentifier,
            "a different identifier came back for the changed answer. The "
            + "console said: " + string.Join(" | ", visitor.Console()));
        var requests = visitor.ClientRequests();
        Assert.AreEqual(
            roundsBefore + 1,
            requests.Count,
            "a change of answer makes exactly one further request. The "
            + "scripts on the page were: "
            + string.Join(", ", visitor.ScriptSources())
            + ". The console said: "
            + string.Join(" | ", visitor.Console())
            + ". The requests were: "
            + string.Join(" || ", requests.Select(r => r.ToString())));
        var changed = requests[requests.Count - 1];
        Assert.AreEqual(
            "personalized",
            changed.Form("id.usage"),
            $"the new answer must be the one sent. The body was: {changed.Body}");
        Assert.AreEqual(
            sequenceBefore + 1,
            Sequence(changed),
            "the sequence goes up by one for the round that carried the "
            + $"changed answer. The body was: {changed.Body}");

        var second = visitor.Identifier();
        AssertDirect(second, "the changed answer was stated directly");
        Assert.AreEqual(
            Usage.Personalized,
            UsageOf(second, "the changed answer was personalized"),
            "the identifier must carry the answer that was actually given.");
        Assert.AreNotEqual(
            firstIdentifier,
            second,
            "a change of answer produces a new identifier.");

        var told = visitor.ChangeIdentifiers();
        Assert.IsTrue(
            told.Contains(second),
            "page code that registered a change handler before the change "
            + "must be told about the new identifier. It was told: "
            + string.Join(", ", told));
    }

    /// <summary>
    /// A change of answer carried across two pages in one tab. The second
    /// page must never serve the visitor the answer they moved away from,
    /// which is the whole point of the record the client script keeps.
    /// <para>
    /// What is asserted is the outcome and not the number of requests. The
    /// stored record is the inputs of the last request, and a page view
    /// whose inputs match it is meant to reuse the answer rather than ask
    /// again, which is the rule about reuse working rather than failing.
    /// Changing the answer on the first page makes that page ask again and
    /// re-record, so the second page's inputs match the new record and it
    /// may legitimately reuse it. Measured against a running service on
    /// 15 September 2026, that is what happens. Asserting a fresh request
    /// here would be asserting that the cache does not work.
    /// </para>
    /// </summary>
    [TestMethod]
    public void ChangeOfAnswer_AcrossPages_TheSecondPageCarriesTheNewAnswer()
    {
        using var server = new PageServer();
        RequireMarketingIdentifiers();
        var page = Pages.Page(
            "Two pages",
            Pages.PlatformTag(new Pages.PlatformSettings()),
            Pages.ClientScriptTag(),
            Pages.ChangeWatcher(Pages.DefaultObjectName));
        server.Put("/one", page);
        server.Put("/two", page);

        using var visitor = NewVisitor(Chrome, server);
        visitor.Go(Harness.SiteA, "/one");
        visitor.WaitForCard("preferences");
        visitor.Press("standard");
        Harness.Until(
            () => visitor.Identifier() != "",
            "the first page produced an identifier. The console said: "
            + string.Join(" | ", visitor.Console()));
        var underStandard = visitor.Identifier();
        Assert.AreEqual(
            Usage.Standard,
            UsageOf(underStandard, "the first answer"),
            "the visitor pressed standard first.");

        // The answer is changed before the visitor leaves the first page.
        visitor.OpenPlatform();
        visitor.WaitForCard("preferences");
        visitor.Press("personalized");
        Harness.Until(
            () => visitor.Identifier() != underStandard
                && visitor.Identifier() != "",
            "the changed answer took effect on the first page. The console "
            + "said: " + string.Join(" | ", visitor.Console()));
        Assert.AreEqual(
            Usage.Personalized,
            UsageOf(visitor.Identifier(), "the changed answer"),
            "the change was to personalized.");

        // The second page, in the same tab.
        visitor.Go(Harness.SiteA, "/two");
        Harness.Until(
            () => visitor.Identifier() != "",
            "the second page settled on an identifier. The console said: "
            + string.Join(" | ", visitor.Console()));
        var onPageTwo = visitor.Identifier();

        Assert.AreEqual(
            Usage.Personalized,
            UsageOf(onPageTwo, "the second page's identifier"),
            "the answer in force when the second page loaded is the one it "
            + "must carry, whether it asked again or reused the record "
            + "written after the change.");
        Assert.AreNotEqual(
            underStandard,
            onPageTwo,
            "the second page must never be serving the identifier made "
            + "under the answer the visitor moved away from.");

        var told = visitor.ChangeIdentifiers();
        Assert.IsTrue(
            told.Contains(onPageTwo),
            "the change handler on the new instance must be told about the "
            + "identifier it settled on, including where that came from the "
            + "record rather than from a fresh request. It was told: "
            + string.Join(", ", told));
    }

    /// <summary>
    /// The alternative answer. It is an answer under the Model Terms like
    /// any other, so it creates an identifier with the direct flag, and
    /// there is nothing to share, so no second card is offered.
    /// </summary>
    [TestMethod]
    public void AlternativeAnswer_CreatesNonMarketingAndFiresTheAction()
    {
        using var server = new PageServer();
        server.Put("/", Pages.Page(
            "Alternative",
            Pages.PlatformTag(new Pages.PlatformSettings()),
            Pages.ClientScriptTag()));

        using var visitor = NewVisitor(Chrome, server);
        visitor.Go(Harness.SiteA, "/");
        visitor.WaitForCard("preferences");
        visitor.Press("alternative");

        Harness.Until(
            visitor.AlternativeFired,
            "the action the publisher configured for the alternative "
            + "button fired. The console said: "
            + string.Join(" | ", visitor.Console()));
        Harness.Until(
            () => visitor.CardVisible("preferences") == false,
            "the dialog closed after the alternative was pressed. The "
            + "console said: " + string.Join(" | ", visitor.Console()));
        Assert.IsFalse(
            visitor.CardVisible("share"),
            "there is nothing to share, so the second card must never be "
            + "offered after the alternative.");

        Harness.Until(
            () => visitor.ClientRequests()
                .Any(r => r.Done && r.Form("id.usage") == "non-marketing"),
            "the alternative answer reached the cloud. The console said: "
            + string.Join(" | ", visitor.Console()));
        var answered = visitor.ClientRequests()
            .Last(r => r.Form("id.usage") == "non-marketing");
        Assert.AreEqual(
            1,
            answered.FormCount("id.usage"),
            $"the answer must be sent once. The body was: {answered.Body}");

        Harness.Until(
            () => visitor.Identifier() != "",
            "an identifier came back for the alternative answer. The "
            + "console said: " + string.Join(" | ", visitor.Console()));
        var identifier = visitor.Identifier();
        AssertDirect(
            identifier,
            "the alternative is an answer the visitor gave directly");
        Assert.AreEqual(
            Usage.NonMarketing,
            UsageOf(identifier, "the alternative answer"),
            "the alternative stores non-marketing.");

        // The framework surface answers (null, false) after the
        // alternative, which is the framework's view of a usage granting
        // no purposes. It is observed here so that the difference between
        // it and what the request carried is on the record.
        Assert.AreEqual(
            "non-marketing",
            visitor.PlatformPreference(),
            "the platform is holding an answer even though its framework "
            + "surface reports none, and the answer is what creates the "
            + "identifier.");
    }

    private static int Sequence(RecordedRequest request)
        => int.TryParse(
            request.Form("sequence"),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;

    private static string Truncate(string value)
        => value.Length <= 300 ? value : value.Substring(0, 300) + "...";
}
