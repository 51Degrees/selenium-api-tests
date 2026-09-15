#nullable enable

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FiftyOne.Did.Model;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// A page with a consent platform on it and no preference platform, which
/// is the other half of demonstration 2 and the whole of demonstration 3's
/// second case, being the identifier whose usage was decoded from a
/// framework string rather than stated.
/// <para>
/// The consent platform here is a stub, and that is the contract rather
/// than a gap. The client script uses only what the framework's
/// specification requires of every platform, being ping,
/// addEventListener and a callback carrying tcString and eventStatus, so a
/// stub honouring those is what every product has to do, and a product
/// that breaks it is the product's defect. The two never share a page, so
/// nothing here also carries the preference platform.
/// </para>
/// </summary>
[TestClass, TestCategory("Browser51Did")]
public class ConsentPlatformAcceptanceTests : Browser51DidTestBase
{
    /// <summary>
    /// The publisher writes nothing but the script tag and the stub the
    /// framework's own specification already tells them to write. The
    /// answer arrives after the script's first round, which is the common
    /// case, and the identifier that comes back records that the usage was
    /// decoded rather than stated.
    /// </summary>
    [Browser51DidTest]
    public void ConsentPlatformOnly_SecondRequestCarriesTheString()
    {
        RequireMarketingIdentifiers();
        using var visitor = NewVisitor(Chrome);
        visitor.Go(Harness.SiteA, Routes.Consent);
        visitor.WaitForClientRounds(1);
        Assert.IsTrue(
            visitor.HasTheNewClientScript(),
            "the page is running a client script with no user prompt block "
            + "in it, so nothing here would be proving the new behaviour.");

        var first = visitor.ClientRequests()[0];
        Assert.IsNull(
            first.Form("id.usage"),
            "no answer has been given yet, and a page with a consent "
            + "platform on it means unknown rather than none, so nothing "
            + $"may be stated. The body was: {first.Body}");
        Assert.AreEqual(
            "",
            visitor.Identifier(),
            "no answer means no identifier.");

        // The visitor answers the consent platform, which delivers the
        // string to whoever registered. The test decides when, so the
        // ordering is asserted rather than timed.
        var listeners = visitor.Script<long>(
            "return window.__51dCmp.deliver();");
        Assert.IsTrue(
            listeners > 0,
            "the client script must have registered a listener with the "
            + "consent platform. Nothing was registered, so the answer "
            + "reached nobody. The console said: "
            + string.Join(" | ", visitor.Console()));

        Harness.Until(
            () => visitor.ClientRequests().Count(r => r.Done) >= 2,
            "the client script asked again once the answer arrived. The "
            + "console said: " + string.Join(" | ", visitor.Console()));

        var second = visitor.ClientRequests().Last();
        Assert.IsNotNull(
            second.Form("tcstring"),
            "the framework string the platform delivered must reach the "
            + $"cloud. The body was: {second.Body}");
        // The demo's stub consent platform delivers one fixed string, and
        // every language's demo has to deliver the same one, so it is
        // named here rather than trusted.
        Assert.AreEqual(
            TcString.Personalized(),
            second.Form("tcstring"),
            "the stub consent platform on the demo's consent page must "
            + "deliver the string granting every purpose, built by "
            + $"TcString.Personalized(). The body was: {second.Body}");
        Assert.IsNull(
            second.Form("id.usage"),
            "the client script must not decide the usage itself. A string "
            + "goes as a string and the server decodes it, which is what "
            + "makes the flag mean something. The body was: "
            + second.Body);
        Assert.AreEqual(
            1,
            second.FormCount("tcstring"),
            "the string must be sent once, because the server takes the "
            + "first value of a repeated key and warns. The body was: "
            + second.Body);

        Harness.Until(
            () => visitor.Identifier() != "",
            "an identifier came back once the answer had been decoded. The "
            + "console said: " + string.Join(" | ", visitor.Console()));
        Assert.IsTrue(
            second.Response.Contains("fodid", StringComparison.Ordinal),
            "the response to the request carrying the string must carry the "
            + "identifier section.");
        AssertFromConsentString(
            visitor.Identifier(),
            "the usage was decoded from the framework string",
            Usage.Personalized);
    }

    /// <summary>
    /// Demonstration 8. A page with nowhere to get an answer from creates
    /// nothing, says so once, and stores a record that carries no answer,
    /// so a later page view with an answer is a different input and asks
    /// again rather than reusing this one.
    /// </summary>
    [Browser51DidTest]
    public void NoPlatformAtAll_NoIdentifierAndOneWarning()
    {
        using var visitor = NewVisitor(Chrome);
        visitor.Go(Harness.SiteA, Routes.NoPlatform);
        visitor.WaitForClientRounds(1);
        Assert.IsTrue(
            visitor.HasTheNewClientScript(),
            "the page is running a client script with no user prompt block "
            + "in it, so it would have nothing to warn about.");

        var requests = visitor.ClientRequests();
        Assert.AreEqual(
            1,
            requests.Count,
            "with nowhere to get an answer from there is nothing to come "
            + "back for, so one request is all there should be. The "
            + "requests were: "
            + string.Join(" || ", requests.Select(r => r.ToString())));
        Assert.IsNull(
            requests[0].Form("id.usage"),
            $"nobody was asked. The body was: {requests[0].Body}");
        Assert.IsNull(
            requests[0].Form("tcstring"),
            $"there is no platform to ask. The body was: {requests[0].Body}");
        Assert.AreEqual(
            "",
            visitor.Identifier(),
            "a page where nobody was asked produces no identifier, which is "
            + "the intended outcome and not a fault.");

        var record = visitor.CacheRecord();
        Assert.IsNotNull(
            record,
            "the client script stores the inputs of the request it made, so "
            + "that a later page view with a different input asks again. "
            + "Nothing was stored.");
        Assert.IsFalse(
            record!.Contains("id.usage", StringComparison.Ordinal),
            "the stored record must carry no answer, or a page view that "
            + "does have one would look the same as this and be served "
            + $"from the cache. The record was: {record}");

        var warnings = visitor.ConsoleMatching(Harness.NoPlatformMessage);
        Assert.AreEqual(
            1,
            warnings.Count,
            "the warning that there is no platform on the page is logged "
            + "once for the page view, not once per round. The console "
            + "said: " + string.Join(" | ", visitor.Console()));
        foreach (var empty in new[] { "undefined", "null" })
        {
            Assert.IsFalse(
                warnings[0].Contains(empty, StringComparison.OrdinalIgnoreCase),
                "the warning names nothing it could not find a value for, "
                + $"so '{empty}' must not appear in it. It said: "
                + warnings[0]);
        }
    }
}
