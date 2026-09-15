#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// Everything the browser acceptance tests share, being where the cloud
/// is, which resource key to use, the two site names a cross site test
/// needs, the browsers, and the guard that stops the whole class running
/// against the old client script.
/// <para>
/// The harness is the same one <c>CrossBrowserContextTests</c> uses, being
/// the container that terminates TLS itself, started by
/// <c>ci/test.ps1</c> with a throwaway resource key and a throwaway
/// context secret. These tests reuse its environment variables rather
/// than adding a second set.
/// </para>
/// </summary>
public static class Harness
{
    /// <summary>
    /// The cloud instance serving TLS itself, for example
    /// https://localhost:8081. Shared with the cross browser context
    /// tests, so one container serves both.
    /// </summary>
    public static readonly string? BaseUrl =
        Environment.GetEnvironmentVariable(
            "FIFTYONE_CONTEXT_SELENIUM_BASEURL");

    /// <summary>
    /// The resource key the pages use.
    /// <para>
    /// It is not the throwaway one the cross browser context tests use.
    /// Creating an identifier for a standard or personalized answer needs
    /// a licence carrying the CloudV5FODiD product, which
    /// <c>DidOnPremiseEngine.TryResolveLicenseId</c> scans the customer's
    /// licence keys for, and refusing without one is deliberate. A
    /// throwaway record cannot have that product, because the product
    /// lives inside a real signed licence key rather than being a name a
    /// record can claim, which is why the context tests ask for the
    /// non-marketing usage. Everything the acceptance tests are for is a
    /// marketing usage, so they use the 51Did entitled resource key that
    /// is already committed in
    /// <c>host/FiftyOne.Pipeline.Cloud.Tests.Common/testConfig.json</c>
    /// as <c>fodid_resource_key</c> and that ci/test.ps1 already uses for
    /// the identifier surface check. That customer's record also carries
    /// a licence key, which is what the shared store needs.
    /// </para>
    /// <para>
    /// It falls back to the context harness key so that a developer who
    /// sets only the two context variables gets a run that says plainly
    /// what it could not do rather than one that will not start.
    /// </para>
    /// </summary>
    public static readonly string? Resource =
        FirstSet(
            "FIFTYONE_BROWSER51DID_RESOURCE",
            "FIFTYONE_CONTEXT_SELENIUM_RESOURCE");

    private static string? FirstSet(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value) == false)
            {
                return value;
            }
        }
        return null;
    }

    /// <summary>
    /// Whether the harness is configured at all. Unset means a developer
    /// ran the project on its own, so the tests report inconclusive rather
    /// than failing, exactly as the context tests do.
    /// </summary>
    public static bool Configured =>
        string.IsNullOrEmpty(BaseUrl) == false
        && string.IsNullOrEmpty(Resource) == false;

    /// <summary>
    /// The first publisher site. It is a name and not localhost, because
    /// the cloud is on localhost and a page there would be the same site
    /// as the cloud, which would make the shared cookie a first party one
    /// and prove nothing.
    /// </summary>
    public const string SiteA = "site-a.localtest";

    /// <summary>
    /// The second publisher site, for the cross site case. A different
    /// registrable name from <see cref="SiteA"/>, so a browser treats the
    /// two as different sites rather than as one.
    /// <para>
    /// The top level domain is one nothing owns, so neither name can ever
    /// resolve on the network and neither can be reached by accident. The
    /// browsers are told to resolve both to this machine, and the
    /// development certificate used when the service is run outside a
    /// container names both, so the same two names work whichever way the
    /// service under test was started.
    /// </para>
    /// </summary>
    public const string SiteB = "site-b.localtest";

    /// <summary>
    /// How long any wait in these tests is given before it is called a
    /// failure. Nothing is asserted on a clock, so this is only the point
    /// at which a test stops waiting for something that is never going to
    /// happen.
    /// </summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    #region Console messages the tests look for

    /// <summary>
    /// Text that exists only in the client script's new user prompt block,
    /// being the message it logs once a page view has reached the server's
    /// maximum number of rounds. The guard below refuses to let any test
    /// in this namespace run unless the script served by the endpoint under
    /// test carries it, which is what makes a green run mean something.
    /// <para>
    /// Settled by the template work package. If that wording changes, this
    /// is the one place to change it, and the report for that package
    /// carries the string in force.
    /// </para>
    /// </summary>
    public const string IterationLimitMessage = "51Degrees: the maximum of";

    /// <summary>
    /// Text that exists only inside the template's user prompt section,
    /// being the name the preference platform puts its own surface under.
    /// The iteration limit message above sits outside that section, so it
    /// says the template is the new one and says nothing about whether the
    /// block was rendered. This says the block is there.
    /// </summary>
    public const string UserPromptBlockMarker = "__51d_pmp";

    /// <summary>
    /// The warning the client script logs when a page carries no place to
    /// get an answer from, so no identifier can be created. Read off the
    /// script the endpoint served on 15 September 2026, where the sentence
    /// is "51Degrees: no preference platform was found on this page. A
    /// platform's stub must precede this script. No 51Did will be created
    /// until a platform answers." Matched as a substring, so the rest of
    /// the sentence may change, and the test using it also asserts that no
    /// value was printed beside it.
    /// </summary>
    public const string NoPlatformMessage =
        "no preference platform was found";

    /// <summary>
    /// The warning the client script logs when a second copy of itself is
    /// loaded onto one page under the same object name, where the sentence
    /// is "51Degrees: fod already exists on this page. Loading the script
    /// twice replaces it. Load it once and call fod.refresh() to update."
    /// The object's name is in it, so only the part that is the same
    /// whatever the object is called is matched. Two tests assert it was
    /// NOT logged, which is how they say that nothing added a second copy.
    /// </summary>
    public const string SecondInstanceWarning =
        "already exists on this page";

    /// <summary>
    /// What the preference platform says when it finds no client script
    /// object on the page. Read from the platform's own source, where the
    /// sentence is "There is no client script object named '{name}' on
    /// this page, so the client script is being added from the cloud that
    /// served this one."
    /// </summary>
    public const string NoClientScriptMessage =
        "no client script object named";

    /// <summary>
    /// And what it says when it goes on to add one, which is the half a
    /// publisher who left the tag out has to see.
    /// </summary>
    public const string AddingClientScriptMessage =
        "client script is being added";

    #endregion

    /// <summary>
    /// The client script for the throwaway resource key, as the page asks
    /// for it.
    /// </summary>
    public static string ClientScriptUrl(string? objectName = null)
    {
        var url = $"{BaseUrl}/api/v4/{Resource}.js";
        return objectName is null
            ? url
            : $"{url}?fod-js-object-name={objectName}";
    }

    /// <summary>The preference platform's loader, as the page asks for it.</summary>
    public static string PlatformLoaderUrl() => $"{BaseUrl}/api/v4/pmp";

    /// <summary>
    /// Server to server, with certificate validation relaxed because the
    /// container under test serves a certificate issued for another name.
    /// The browsers are told to accept it for the same reason.
    /// </summary>
    private static readonly HttpClient Reader = new(
        new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                (message, certificate, chain, errors) => true,
        })
    {
        Timeout = TimeSpan.FromSeconds(60),
    };

    private static readonly object GuardLock = new();
    private static string? _guardEvidence;
    private static string? _guardFailure;

    /// <summary>
    /// The guard. Fetches the client script from the endpoint the tests are
    /// about to drive a browser at and refuses to go on unless the body
    /// carries both of the markers below.
    /// <para>
    /// Two markers, because they say different things. The iteration limit
    /// message says the template is the new one. It sits outside the user
    /// prompt section, so it is in the rendered script whenever updates
    /// are enabled, whether or not the block itself was rendered. The
    /// platform's own global name appears only inside the section, so it
    /// is what says the block is actually there. A run that had only the
    /// first would be testing the new template with the block switched
    /// off, which passes nothing it is meant to prove.
    /// </para>
    /// <para>
    /// It runs once per class from the set up rather than inside a test, so
    /// no test here can run against the old script and report green. A run
    /// against a cloud built on the released 4.5.104 package fails every
    /// test in the class with the line below, which says what was served
    /// and what was wanted.
    /// </para>
    /// </summary>
    public static void RequireTheNewClientScript()
    {
        lock (GuardLock)
        {
            if (_guardFailure != null)
            {
                Assert.Fail(_guardFailure);
            }
            if (_guardEvidence != null)
            {
                Console.WriteLine(_guardEvidence);
                return;
            }
            string body;
            var url = ClientScriptUrl();
            try
            {
                body = Reader.GetStringAsync(url).Result;
            }
            catch (Exception error)
            {
                _guardFailure =
                    "The client script could not be fetched from "
                    + $"{Redacted(url)}, "
                    + "so there is no way to tell which template the "
                    + $"container was built from. {error.Message}";
                Assert.Fail(_guardFailure);
                return;
            }
            var evidence = new List<string>();
            foreach (var (marker, says) in RequiredMarkers)
            {
                var found = body.IndexOf(marker, StringComparison.Ordinal);
                if (found < 0)
                {
                    _guardFailure =
                        "The client script served by "
                        + $"{Redacted(url)} does not carry "
                        + $"'{marker}', which is what says {says}. Every "
                        + "test in this class would be proving the wrong "
                        + "thing, so none of them runs. Move the "
                        + "FiftyOne.Pipeline.JavaScriptBuilder pin to the "
                        + "package built from the new template and build "
                        + $"the image again. The script was {body.Length} "
                        + "bytes.";
                    Assert.Fail(_guardFailure);
                    return;
                }
                evidence.Add(
                    $"'{marker}' at {found}: {Around(body, found)}");
            }
            _guardEvidence =
                "Client script guard passed. "
                + $"{Redacted(url)} served {body.Length} "
                + "bytes carrying " + string.Join(" and ", evidence);
            Console.WriteLine(_guardEvidence);
        }
    }

    /// <summary>
    /// What the served client script has to carry, and what each one
    /// proves. See <see cref="RequireTheNewClientScript"/> for why one is
    /// not enough.
    /// </summary>
    private static readonly (string Marker, string Says)[] RequiredMarkers =
    {
        (IterationLimitMessage, "the template is the new one"),
        (UserPromptBlockMarker, "the user prompt block was rendered"),
    };

    /// <summary>
    /// The guard's evidence, for a report that has to quote it. Null until
    /// <see cref="RequireTheNewClientScript"/> has run and passed.
    /// </summary>
    public static string? GuardEvidence => _guardEvidence;

    /// <summary>
    /// Asks a check until it is happy or until it has asked enough times
    /// to say the answer is settled.
    /// <para>
    /// A service that has just come up answers the first request of a kind
    /// before everything behind it is warm, and one cold answer is not a
    /// refusal. A real refusal reads the same every time, so this costs a
    /// few seconds there and rescues a run that would otherwise skip every
    /// test on a first answer nobody would have accepted.
    /// </para>
    /// </summary>
    private static string? Settled(Func<string?> check)
    {
        string? refusal = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            refusal = check();
            if (refusal == null)
            {
                return null;
            }
            Thread.Sleep(2000);
        }
        return refusal;
    }

    private static string? _marketingRefusal;
    private static bool _marketingChecked;

    /// <summary>
    /// Refuses to go on where this resource key cannot create an
    /// identifier for a marketing answer, which is what standard and
    /// personalized both are.
    /// <para>
    /// The service is asked rather than a flag being read, so the reason
    /// the test reports is the service's own words rather than a guess,
    /// and a key that stops carrying the product later says so instead of
    /// failing somewhere in a browser.
    /// </para>
    /// </summary>
    public static void RequireMarketingIdentifiers()
    {
        lock (GuardLock)
        {
            if (_marketingChecked == false)
            {
                _marketingChecked = true;
                _marketingRefusal = Settled(MarketingRefusal);
            }
        }
        if (_marketingRefusal != null)
        {
            Assert.Inconclusive(
                "This resource key cannot create an identifier for a "
                + "marketing answer, which is what standard and "
                + "personalized both are, so nothing here would be "
                + "testing the thing it is for. The service said: "
                + _marketingRefusal
                + " Point FIFTYONE_BROWSER51DID_RESOURCE at a resource key "
                + "whose customer holds a licence carrying the CloudV5FODiD "
                + "product, such as fodid_resource_key in "
                + "host/FiftyOne.Pipeline.Cloud.Tests.Common/testConfig.json.");
        }
    }

    /// <summary>
    /// Asks the service to make a standard identifier and reports why it
    /// would not, or null where it did.
    /// </summary>
    private static string? MarketingRefusal()
    {
        var url = $"{BaseUrl}/api/v4/json?resource={Resource}"
            + "&id.usage=standard&values=FODiD.IdProbGlobal";
        string body;
        try
        {
            body = Reader.GetStringAsync(url).Result;
        }
        catch (Exception error)
        {
            return $"the service could not be asked. {error.Message}";
        }
        JsonElement fodid;
        try
        {
            var root = JsonDocument.Parse(body).RootElement;
            if (root.TryGetProperty("fodid", out fodid) == false)
            {
                return "the response carried no fodid section at all, so "
                    + "the key is not entitled to the identifier "
                    + "properties.";
            }
        }
        catch (JsonException)
        {
            return $"the response was not readable. {Truncate(body)}";
        }
        if (fodid.TryGetProperty("idprobglobal", out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return null;
        }
        return fodid.TryGetProperty(
            "idprobglobalnullreason", out var reason)
            ? reason.GetString()
            : "no identifier came back and no reason was given.";
    }

    private static string? _sharedStoreRefusal;
    private static bool _sharedStoreChecked;

    /// <summary>
    /// Refuses to go on where the shared store will not take a write for
    /// this resource key. The store's cookie is named from the customer's
    /// licence key, and the controller answers a record carrying none with
    /// one refusal, so asking it once is what tells the tests whether a
    /// choice can be carried between sites at all.
    /// </summary>
    public static void RequireSharedStore()
    {
        lock (GuardLock)
        {
            if (_sharedStoreChecked == false)
            {
                _sharedStoreChecked = true;
                _sharedStoreRefusal = Settled(SharedStoreRefusal);
            }
        }
        if (_sharedStoreRefusal != null)
        {
            Assert.Inconclusive(
                "The cloud will not hold a choice for this resource key, "
                + "so a choice cannot be carried between sites and nothing "
                + "here would be testing that it is. The service said: "
                + _sharedStoreRefusal);
        }
    }

    /// <summary>
    /// Offers the shared store a write and reports why it would not take
    /// it, or null where it did. Nothing is kept, because the answer's
    /// cookie goes to this client rather than to any browser and this
    /// client holds none.
    /// </summary>
    private static string? SharedStoreRefusal()
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{BaseUrl}/api/v4/pmp/pref")
            {
                Content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["resource"] = Resource ?? string.Empty,
                        ["preference"] = "standard",
                        ["network"] = NetworkName,
                    }),
            };
            request.Headers.Referrer = new Uri($"http://{SiteA}/");
            using var response = Reader.Send(request);
            if (response.IsSuccessStatusCode)
            {
                return null;
            }
            var body = response.Content.ReadAsStringAsync().Result;
            return $"{(int)response.StatusCode} {Truncate(body)}";
        }
        catch (Exception error)
        {
            return error.Message;
        }
    }

    /// <summary>
    /// The group of sites a choice is shared across in these tests. The
    /// name is what the cookie holding the choice is derived from, so the
    /// pages and the check above have to use the same one.
    /// </summary>
    public const string NetworkName = "Fifty One Network";

    private static string Truncate(string value)
        => value.Length <= 300 ? value : value.Substring(0, 300) + "...";

    /// <summary>
    /// A URL with the resource key taken out of it, for anything a test
    /// prints. A failure message and the guard's evidence both end up in a
    /// run log and in a pull request body, and a resource key is a
    /// credential that must never be written down anywhere.
    /// </summary>
    public static string Redacted(string url)
        => string.IsNullOrEmpty(Resource)
            ? url
            : url.Replace(Resource, "<resource key>", StringComparison.Ordinal);

    /// <summary>One line of the script either side of the match.</summary>
    private static string Around(string body, int at)
    {
        var from = Math.Max(0, at - 40);
        var to = Math.Min(body.Length, at + 120);
        return body.Substring(from, to - from)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }

    #region Browsers

    /// <summary>
    /// Chrome, with both publisher site names pointed at this machine.
    /// Nothing is added to the hosts file and nothing has to resolve on
    /// the network, which is what makes the cross site case run on any
    /// runner.
    /// </summary>
    public static IWebDriver NewChrome(
        IReadOnlyDictionary<string, object>? preferences = null,
        params string[] extraArguments)
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument(
            "--host-resolver-rules="
            + $"MAP {SiteA} 127.0.0.1,MAP {SiteB} 127.0.0.1");
        options.AcceptInsecureCertificates = true;
        foreach (var argument in extraArguments)
        {
            options.AddArgument(argument);
        }
        if (preferences != null)
        {
            foreach (var preference in preferences)
            {
                options.AddUserProfilePreference(
                    preference.Key, preference.Value);
            }
        }
        return new ChromeDriver(options);
    }

    /// <summary>
    /// Firefox, with the same two site names pointed at this machine.
    /// Firefox has no host resolver rules, so network.dns.localDomains is
    /// used, which is the setting that does the same job.
    /// </summary>
    public static IWebDriver NewFirefox(
        IReadOnlyDictionary<string, object>? preferences = null)
    {
        var options = new FirefoxOptions { AcceptInsecureCertificates = true };
        options.AddArgument("-headless");
        options.SetPreference("network.dns.localDomains", $"{SiteA},{SiteB}");
        // Third party cookies are what the shared choice travels on, so
        // the tests state the behaviour they are written for rather than
        // inheriting whatever the installed build defaults to. 0 is
        // "accept all", and the pair below is Firefox's own total cookie
        // protection, which would otherwise keep the cloud's cookie in a
        // separate jar per site and hide a working exchange.
        options.SetPreference("network.cookie.cookieBehavior", 0);
        options.SetPreference(
            "privacy.partition.network_state.ocsp_cache", false);
        if (preferences != null)
        {
            foreach (var preference in preferences)
            {
                switch (preference.Value)
                {
                    case bool flag:
                        options.SetPreference(preference.Key, flag);
                        break;
                    case int number:
                        options.SetPreference(preference.Key, number);
                        break;
                    default:
                        options.SetPreference(
                            preference.Key,
                            Convert.ToString(
                                preference.Value,
                                CultureInfo.InvariantCulture));
                        break;
                }
            }
        }
        return new FirefoxDriver(options);
    }

    /// <summary>
    /// Waits for a condition the page reports, polling rather than
    /// sleeping, so a test never asserts on a clock. The message is what
    /// the failure says, so it names what never happened.
    /// </summary>
    public static void Until(
        Func<bool> condition, string whatWasWaitedFor)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }
            Thread.Sleep(100);
        }
        Assert.Fail(
            $"Waited {Patience.TotalSeconds:0} seconds and "
            + $"{whatWasWaitedFor} never happened.");
    }

    #endregion
}
