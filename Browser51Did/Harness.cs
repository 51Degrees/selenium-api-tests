#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using FiftyOne.Pipeline.Cloud.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// Everything the browser acceptance tests share, being where the cloud
/// is, which resource key to use, the two site names a cross site test
/// needs, the browsers, and the guard that stops a test running against
/// the old client script.
/// <para>
/// The thing under test is a demo, a web app serving the pages these tests
/// load, which every language mirrors, started with a cloud endpoint and a
/// resource key. The preference platform and the shared store come from
/// that cloud whichever demo is chosen. See <see cref="Demo"/>.
/// </para>
/// </summary>
public static class Harness
{
    /// <summary>
    /// A setting read through the suite's own <see cref="TestConfig"/>,
    /// with the message it gives where the variable is missing.
    /// </summary>
    private readonly record struct Setting(string? Value, string? Missing);

    private static Setting Read(Func<string> require)
    {
        try
        {
            return new Setting(require(), null);
        }
        catch (InvalidOperationException missing)
        {
            return new Setting(null, missing.Message);
        }
    }

    private static readonly Setting Endpoint =
        Read(() => TestConfig.Instance().DemoCloudEndpoint);

    private static readonly Setting ResourceKey =
        Read(() => TestConfig.Instance().DemoResourceKey);

    /// <summary>
    /// The cloud, for example https://localhost:5443, with no trailing
    /// slash, taken from 51DEGREES_CLOUD_ENDPOINT, the variable the demo is
    /// started with. That endpoint names the api/v4 path, as every reader of
    /// it expects, and the path is taken off here, because what the tests
    /// ask the cloud directly they build from its root. An endpoint written
    /// without the path is taken as the root.
    /// </summary>
    public static string? CloudUrl
    {
        get
        {
            if (string.IsNullOrEmpty(Endpoint.Value))
            {
                return null;
            }
            var root = Endpoint.Value.TrimEnd('/');
            const string ApiPath = "/api/v4";
            return root.EndsWith(ApiPath, StringComparison.OrdinalIgnoreCase)
                ? root.Substring(0, root.Length - ApiPath.Length)
                : root;
        }
    }

    /// <summary>
    /// The resource key the demo is started with, from
    /// 51DEGREES_RESOURCE_KEY, the variable every language's demo reads
    /// first, or where that is unset from _51DEGREES_RESOURCE_KEY_51DID,
    /// the name continuous integration sets. The demo is given it as
    /// 51DEGREES_RESOURCE_KEY either way.
    /// <para>
    /// The tests that create a 51Did for a standard or personalized answer
    /// need a resource key whose products include CloudV5FODiD.
    /// <see cref="RequireMarketingIdentifiers"/> asks the service first,
    /// and where the key cannot create, those tests skip with the service's
    /// own reason.
    /// </para>
    /// </summary>
    public static string? Resource => ResourceKey.Value;

    /// <summary>
    /// Whether the harness is configured at all. Unset means a developer
    /// ran the category without saying where the cloud is or which key to
    /// use, so the tests report inconclusive rather than failing, the way
    /// the suite's other tests do when their configuration is missing.
    /// </summary>
    public static bool Configured =>
        string.IsNullOrEmpty(CloudUrl) == false
        && string.IsNullOrEmpty(Resource) == false;

    /// <summary>Why the harness is not configured, for the skip.</summary>
    public static string NotConfiguredReason =>
        "The 51Did browser acceptance tests are not configured. "
        + string.Join(
            " ",
            new[]
            {
                string.IsNullOrEmpty(CloudUrl) ? Endpoint.Missing : null,
                string.IsNullOrEmpty(Resource) ? ResourceKey.Missing : null,
            }.Where(missing => missing != null))
        + " 51DEGREES_CLOUD_ENDPOINT names the cloud including its api/v4 "
        + "path, and 51DEGREES_RESOURCE_KEY, or where that is unset "
        + "_51DEGREES_RESOURCE_KEY_51DID, a resource key the cloud "
        + "creates 51Dids for standard and personalized answers with. Both "
        + "are handed to the demo under the first names. See "
        + "Browser51Did/README.md.";

    /// <summary>
    /// The object name a page uses when it does not ask for another one,
    /// which is what the client script and the preference platform both
    /// fall back to.
    /// </summary>
    public const string DefaultObjectName = "fod";

    /// <summary>
    /// The object name the demo's <see cref="Routes.NamedObject"/> page
    /// gives the preference platform in data-object-name.
    /// </summary>
    public const string NamedObject = "fiftyOneData";

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
    /// in this namespace run unless the script served by the implementation
    /// under test carries it, which is what makes a green run mean
    /// something.
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

    /// <summary>The preference platform's loader, as the page asks for it.</summary>
    public static string PlatformLoaderUrl() => $"{CloudUrl}/api/v4/pmp";

    /// <summary>
    /// Server to server, with certificate validation relaxed because the
    /// cloud under test may serve a certificate issued for another name.
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
    /// The guard. Fetches the client script the demo's pages load
    /// before the tests drive a browser at them, and refuses to go on unless
    /// the body carries both of the markers below.
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
    /// It runs once per class from the set up rather than inside a test,
    /// so no test here can run against the old script and report green. A
    /// run against a cloud built on the released 4.5.104 package fails every
    /// test in the class with the line below, which says what was served
    /// and what was wanted. The answer is kept, so it is asked once a run.
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
            var implementation = Demo.Chosen;
            var url = implementation.ClientScriptFetchUrl();
            string body;
            try
            {
                body = Reader.GetStringAsync(url).Result;
            }
            catch (Exception error)
            {
                _guardFailure =
                    "The client script could not be fetched from "
                    + $"{Redacted(url)}, "
                    + "so there is no way to tell which template "
                    + $"{implementation.Name} was built from. "
                    + Redacted(error.Message);
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
                        + $"{implementation.Name} at {Redacted(url)} does "
                        + $"not carry '{marker}', which is what says "
                        + $"{says}. Every test would be proving the wrong "
                        + "thing, so none of them runs. "
                        + Remedy(implementation)
                        + $" The script was {body.Length} bytes.";
                    Assert.Fail(_guardFailure);
                    return;
                }
                evidence.Add(
                    $"'{marker}' at {found}: {Around(body, found)}");
            }
            _guardEvidence =
                $"Client script guard passed for {implementation.Name}. "
                + $"{Redacted(url)} served {body.Length} "
                + "bytes carrying " + string.Join(" and ", evidence);
            Console.WriteLine(_guardEvidence);
        }
    }

    /// <summary>What to do about a client script without the markers.</summary>
    private static string Remedy(Demo demo)
        => demo.Mode == DemoMode.Cloud
            ? "Move the cloud's FiftyOne.Pipeline.JavaScriptBuilder pin to "
                + "the package built from the new template and build the cloud "
                + "again."
            : $"Build {demo.Name} against a JavaScript builder carrying the "
                + "new template, with a 51Did element in its pipeline, which "
                + "is what makes the user prompt block render, and start it "
                + "again.";

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
    /// The cloud is asked rather than a flag being read, so the reason the
    /// test reports is the service's own words rather than a guess, and a
    /// key that stops carrying the product later says so instead of
    /// failing somewhere in a browser. It is the cloud that is asked
    /// whichever implementation is under test, because an example creates
    /// nothing itself and passes the request on to the cloud.
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
                + Redacted(_marketingRefusal)
                + " Point 51DEGREES_RESOURCE_KEY, or in continuous "
                + "integration _51DEGREES_RESOURCE_KEY_51DID, at a "
                + "resource key the service creates standard identifiers "
                + "for.");
        }
    }

    /// <summary>
    /// Asks the cloud to make a standard identifier and reports why it
    /// would not, or null where it did.
    /// </summary>
    private static string? MarketingRefusal()
    {
        var url = $"{CloudUrl}/api/v4/json?resource={Resource}"
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
                + Redacted(_sharedStoreRefusal));
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
                HttpMethod.Post, $"{CloudUrl}/api/v4/pmp/pref")
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
    /// Refuses to go on where a browser would not keep the shared choice's
    /// cookie at all, so a choice could not be carried between sites
    /// whatever the code did.
    /// <para>
    /// The cloud sets that cookie Secure and SameSite=None, and a browser
    /// keeps such a cookie only from a secure origin. HTTPS is one, and so
    /// is localhost, which browsers treat as secure whatever the scheme.
    /// Measured on 15 September 2026 against a service on
    /// http://localhost:5050, both browsers kept the cookie and both tests
    /// that call this passed. So what is refused is a cloud reached over
    /// plain HTTP by any other name, and the suite's usual target,
    /// http://localhost:8080, is not refused. Other loopback names such as
    /// 127.0.0.1 were not measured, so they are refused rather than
    /// assumed.
    /// </para>
    /// </summary>
    public static void RequireSecureCookies()
    {
        var cloud = new Uri(CloudUrl!);
        if (cloud.Scheme == Uri.UriSchemeHttps
            || string.Equals(
                cloud.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        Assert.Inconclusive(
            $"The cloud is at {Redacted(CloudUrl!)}, which is plain HTTP "
            + "and not localhost, so a browser will not keep the shared "
            + "choice's cookie, which the cloud sets Secure and "
            + "SameSite=None. A choice cannot be carried between sites "
            + "there, so nothing here would be testing that it is. Point "
            + "51DEGREES_CLOUD_ENDPOINT at the cloud's HTTPS listener to run "
            + "this.");
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
    /// Text with the resource key taken out of it, for anything a test
    /// prints. A failure message and the guard's evidence both end up in a
    /// run log and in a pull request body, this suite runs in public
    /// repositories' builds, and a resource key is a credential that must
    /// never be written down anywhere.
    /// </summary>
    public static string Redacted(string text)
        => string.IsNullOrEmpty(Resource)
            ? text
            : text.Replace(Resource, "<resource key>", StringComparison.Ordinal);

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
        return Start(options);
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
                                CultureInfo.InvariantCulture) ?? string.Empty);
                        break;
                }
            }
        }
        return Start(options);
    }

    /// <summary>
    /// A local browser, or one on the Selenium grid SELENIUM_URL names,
    /// which is how every browser in this suite is started. A grid has to
    /// share this machine's network, because the site names resolve to
    /// 127.0.0.1 inside the browser.
    /// </summary>
    private static IWebDriver Start(DriverOptions options)
    {
        if (ExternalSeleniumHelper.IsExternalSelenium(out var seleniumUrl))
        {
            ExternalSeleniumHelper.AddExternalSeleniumArguments(options);
            return new RemoteWebDriver(new Uri(seleniumUrl), options);
        }
        return options switch
        {
            ChromeOptions chrome => new ChromeDriver(chrome),
            FirefoxOptions firefox => new FirefoxDriver(firefox),
            _ => throw new ArgumentOutOfRangeException(
                nameof(options),
                $"{options.GetType().Name} is not a browser these tests use."),
        };
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
        Assert.Fail(Redacted(
            $"Waited {Patience.TotalSeconds:0} seconds and "
            + $"{whatWasWaitedFor} never happened."));
    }

    #endregion
}
