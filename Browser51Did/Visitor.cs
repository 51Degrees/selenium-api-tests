#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>One request a page made, as the page itself saw it.</summary>
public sealed class RecordedRequest
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("method")] public string Method { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("body")] public string Body { get; set; } = "";
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("response")] public string Response { get; set; } = "";
    [JsonPropertyName("done")] public bool Done { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }

    /// <summary>
    /// The value of a form key in the request body, or null where the key
    /// is absent. The body is form encoded with spaces as plus signs,
    /// which is what the client script sends.
    /// </summary>
    public string? Form(string key)
    {
        foreach (var pair in Body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.IndexOf('=');
            var name = split < 0 ? pair : pair.Substring(0, split);
            if (Uri.UnescapeDataString(name.Replace('+', ' ')) != key)
            {
                continue;
            }
            var value = split < 0 ? "" : pair.Substring(split + 1);
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        return null;
    }

    /// <summary>How many times a form key appears in the body.</summary>
    public int FormCount(string key)
        => Body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Count(pair =>
            {
                var split = pair.IndexOf('=');
                var name = split < 0 ? pair : pair.Substring(0, split);
                return Uri.UnescapeDataString(name.Replace('+', ' ')) == key;
            });

    public override string ToString()
        => $"{Kind} {Method} {Url} status={Status} done={Done} "
            + $"body={Body}";
}

/// <summary>
/// One visitor, being a browser and the publisher's website it is looking
/// at. Everything a test asserts on is read back through here, so an
/// assertion reads as a sentence about the page rather than as a lump of
/// JavaScript.
/// </summary>
public sealed class Visitor : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Finding the preference platform's dialog. It renders into a shadow
    /// root attached to a plain div with no name of its own, so the root
    /// is found by what is inside it.
    /// </summary>
    private const string Prelude = @"
      function pmpRoot() {
        var all = document.querySelectorAll('*');
        for (var i = 0; i < all.length; i++) {
          var r = all[i].shadowRoot;
          if (r && r.querySelector('.pmp')) { return r; }
        }
        return null;
      }
      // Whether a visitor can actually see something. offsetParent alone
      // is not enough, because a browser reports null for it on anything
      // positioned fixed, which the dialog is, and the answer would then
      // be no for a card that is plainly on the screen.
      function visible(el) {
        if (!el) { return false; }
        var style = window.getComputedStyle(el);
        if (style.display === 'none'
            || style.visibility === 'hidden'
            || Number(style.opacity) === 0) {
          return false;
        }
        var box = el.getBoundingClientRect();
        return box.width > 0 && box.height > 0;
      }
    ";

    public IWebDriver Driver { get; }

    public PageServer Server { get; }

    /// <summary>The browser's name, for a failure message.</summary>
    public string BrowserName { get; }

    public Visitor(IWebDriver driver, PageServer server, string browserName)
    {
        Driver = driver;
        Server = server;
        BrowserName = browserName;
    }

    /// <summary>Loads a page and waits for the document to settle.</summary>
    public void Go(string site, string path)
    {
        Driver.Navigate().GoToUrl(Server.UrlFor(site, path));
        Harness.Until(
            () => Script<string>("return document.readyState;") == "complete",
            $"the page at {site}{path} finished loading");
    }

    public T? Script<T>(string body, params object[] arguments)
    {
        var result = ((IJavaScriptExecutor)Driver)
            .ExecuteScript(Prelude + body, arguments);
        if (result is null)
        {
            return default;
        }
        if (result is T typed)
        {
            return typed;
        }
        return (T)Convert.ChangeType(
            result, typeof(T), CultureInfo.InvariantCulture);
    }

    private string Read(string body)
        => Script<string>(body) ?? "[]";

    #region What the page sent and was told

    /// <summary>Every request the page made, oldest first.</summary>
    public IReadOnlyList<RecordedRequest> Requests()
        => JsonSerializer.Deserialize<List<RecordedRequest>>(
            Read("return JSON.stringify("
                + "(window.__51dTest && window.__51dTest.requests) || []);"),
            Json) ?? new List<RecordedRequest>();

    /// <summary>
    /// The client script's own requests, being the ones to the endpoint it
    /// posts its evidence to.
    /// </summary>
    public IReadOnlyList<RecordedRequest> ClientRequests()
        => Requests()
            .Where(r => r.Url.Contains(
                "/api/v4/json", StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// The preference platform's calls to the shared store, being the read
    /// on load and the write when a visitor agrees to share.
    /// </summary>
    public IReadOnlyList<RecordedRequest> SharedStoreRequests()
        => Requests()
            .Where(r => r.Url.Contains(
                "/api/v4/pmp/pref", StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>Every console line, in the order they were written.</summary>
    public IReadOnlyList<string> Console()
        => JsonSerializer.Deserialize<List<ConsoleLine>>(
                Read("return JSON.stringify("
                    + "(window.__51dTest && window.__51dTest.console) || []);"),
                Json)
            ?.Select(line => $"[{line.Level}] {line.Text}")
            .ToList()
            ?? new List<string>();

    private sealed class ConsoleLine
    {
        [JsonPropertyName("level")] public string Level { get; set; } = "";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }

    /// <summary>Console lines carrying the given text.</summary>
    public IReadOnlyList<string> ConsoleMatching(string text)
        => Console()
            .Where(line => line.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>The preferences the platform's action URL was fired with.</summary>
    public IReadOnlyList<string> Actions()
        => JsonSerializer.Deserialize<List<string>>(
            Read("return JSON.stringify("
                + "(window.__51dTest && window.__51dTest.actions) || []);"),
            Json) ?? new List<string>();

    /// <summary>Whether the alternative button's own action fired.</summary>
    public bool AlternativeFired()
        => Script<bool>(
            "return !!(window.__51dTest && window.__51dTest.altFired);");

    /// <summary>
    /// The identifiers handed to a change handler the page registered,
    /// oldest first.
    /// </summary>
    public IReadOnlyList<string> ChangeIdentifiers()
        => JsonSerializer.Deserialize<List<Dictionary<string, string?>?>>(
                Read("return JSON.stringify("
                    + "(window.__51dTest && window.__51dTest.changes) || []);"),
                Json)
            ?.Select(section =>
                section != null
                && section.TryGetValue("idprobglobal", out var value)
                    ? value ?? ""
                    : "")
            .ToList()
            ?? new List<string>();

    /// <summary>Every key the page has in its own local storage.</summary>
    public IReadOnlyList<string> LocalStorageKeys()
        => JsonSerializer.Deserialize<List<string>>(
            Read(@"
              var keys = [];
              try {
                for (var i = 0; i < localStorage.length; i++) {
                  keys.push(localStorage.key(i));
                }
              } catch (e) { }
              return JSON.stringify(keys);"),
            Json) ?? new List<string>();

    #endregion

    #region The client script's object

    /// <summary>Whether the client script's object exists on the page.</summary>
    public bool HasClientObject(string objectName = Pages.DefaultObjectName)
        => Script<bool>(
            $"return typeof window['{objectName}'] === 'object'"
            + $" && window['{objectName}'] !== null;");

    /// <summary>
    /// The global identifier the client script is holding, or an empty
    /// string when it holds none. An identifier is created only once an
    /// answer has reached the cloud, so an empty string is the normal
    /// state of a first request that carried no answer.
    /// </summary>
    public string Identifier(string objectName = Pages.DefaultObjectName)
        => Script<string>(
            $@"var o = window['{objectName}'];
               return (o && o.fodid && o.fodid.idprobglobal) || '';") ?? "";

    /// <summary>
    /// Whether the object carries the user prompt block's own entry point,
    /// which exists only in the new client script. This is the guard's
    /// check made again on what the browser actually loaded, rather than
    /// on what the endpoint served the test host.
    /// </summary>
    public bool HasTheNewClientScript(
        string objectName = Pages.DefaultObjectName)
        => Script<bool>(
            $"var o = window['{objectName}'];"
            + " return !!(o && typeof o.refresh === 'function');");

    /// <summary>
    /// The third party cookie result the client script resolved, as the
    /// page sees it. Empty where the property has no value.
    /// </summary>
    public string ThirdPartyCookies(
        string objectName = Pages.DefaultObjectName)
        => Script<string>(
            $@"var o = window['{objectName}'];
               var d = o && o.device;
               var v = d && d.thirdpartycookiesenabled;
               return v === undefined || v === null ? '' : String(v);") ?? "";

    /// <summary>
    /// The names of the snippet results the client script has stored for
    /// this page view, which are what it must send with the request that
    /// creates an identifier. Read from the page's own session storage,
    /// where the script puts them, so the test does not have to know which
    /// properties the resource key happens to carry.
    /// </summary>
    public IReadOnlyList<string> SnippetValueNames(
        string objectName = Pages.DefaultObjectName)
        => JsonSerializer.Deserialize<List<string>>(
            Read($@"
              var prefix = '{objectName}_data_';
              var names = [];
              try {{
                for (var i = 0; i < sessionStorage.length; i++) {{
                  var key = sessionStorage.key(i);
                  if (key.indexOf(prefix) === 0) {{
                    names.push(key.substring(prefix.length));
                  }}
                }}
              }} catch (e) {{ }}
              return JSON.stringify(names);"),
            Json) ?? new List<string>();

    /// <summary>
    /// The record of the last request's inputs the client script keeps, so
    /// that a later page view with a different input asks again rather
    /// than answering from the cache. It is stored as a plain string, and
    /// null where nothing has been stored.
    /// <para>
    /// The key is found by its ending rather than spelled out, so a
    /// difference in naming between the template and this test shows up as
    /// a failed assertion about the contents rather than as a test that
    /// silently checks nothing.
    /// </para>
    /// </summary>
    public string? CacheRecord(string objectName = Pages.DefaultObjectName)
    {
        var value = Script<string>($@"
            try {{
              for (var i = 0; i < sessionStorage.length; i++) {{
                var key = sessionStorage.key(i);
                if (key.indexOf('{objectName}') === 0
                    && key.indexOf('_inputs') === key.length - 7) {{
                  return sessionStorage.getItem(key);
                }}
              }}
            }} catch (e) {{ }}
            return null;");
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>
    /// Whether a card is showing and how many rounds the client script has
    /// finished, read together in one call, so a test can say that one was
    /// true while the other had not moved. Two separate reads would leave a
    /// gap in which the answer could change, and the ordering is the whole
    /// point of the assertion.
    /// </summary>
    public (bool CardVisible, int RoundsDone) Observe(string card)
    {
        var answer = Script<string>($@"
            var root = pmpRoot();
            var el = root
              ? root.querySelector('[data-card=""{card}""]') : null;
            var showing = visible(el);
            var done = 0;
            var all = (window.__51dTest && window.__51dTest.requests) || [];
            for (var i = 0; i < all.length; i++) {{
              if (all[i].done
                  && all[i].url.indexOf('/api/v4/json') !== -1) {{
                done++;
              }}
            }}
            return (showing ? '1' : '0') + '|' + done;") ?? "0|0";
        var parts = answer.Split('|');
        return (parts[0] == "1", int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Whether the visitor is somewhere the regulation applies, as the
    /// client script resolved it. Empty where the property has no value,
    /// which is what a resource key that does not carry it produces and
    /// what every key produces until the change adding it is released.
    /// </summary>
    public string IsGdpr(string objectName = Pages.DefaultObjectName)
        => Script<string>(
            $@"var o = window['{objectName}'];
               var groups = ['derived', 'device'];
               for (var i = 0; i < groups.length; i++) {{
                 var g = o && o[groups[i]];
                 var v = g && g.isgdpr;
                 if (v !== undefined && v !== null) {{ return String(v); }}
               }}
               return '';") ?? "";

    /// <summary>
    /// Every script element on the page, by source. Used where a test has
    /// to see that something added one.
    /// </summary>
    public IReadOnlyList<string> ScriptSources()
        => JsonSerializer.Deserialize<List<string>>(
            Read(@"
              return JSON.stringify(
                Array.prototype.slice
                  .call(document.querySelectorAll('script'))
                  .map(function (s) { return s.src || ''; })
                  .filter(function (s) { return s !== ''; }));"),
            Json) ?? new List<string>();

    #endregion

    #region The preference platform

    /// <summary>Whether the platform has put anything on the page yet.</summary>
    public bool PlatformLoaded() => Script<bool>("return pmpRoot() !== null;");

    /// <summary>Whether a named card is on the page and visible.</summary>
    public bool CardVisible(string card)
        => Script<bool>($@"
            var root = pmpRoot();
            if (!root) {{ return false; }}
            return visible(root.querySelector('[data-card=""{card}""]'));");

    /// <summary>
    /// Whether the floating button is showing and no card is, which is
    /// what a visitor who has already answered sees.
    /// <para>
    /// Both templates are put on the page at once and the dialog is hidden
    /// by a class on the container rather than being taken away, so this
    /// asks what is visible rather than what exists.
    /// </para>
    /// <para>
    /// The button is found by what it does rather than by its class,
    /// because the build renames every class in the stylesheet to one or
    /// two letters (pmp/dist/class-map.json turns pmp-fab into y and
    /// pmp-popup into ag), so a test written against the class names in
    /// the source finds nothing in the bundle a publisher is actually
    /// served. The data attributes are not renamed.
    /// </para>
    /// </summary>
    public bool BubbleOnly()
        => Script<bool>(@"
            var root = pmpRoot();
            if (!root) { return false; }
            if (!visible(root.querySelector('[data-action=""open""]'))) {
              return false;
            }
            var cards = root.querySelectorAll('[data-card]');
            for (var i = 0; i < cards.length; i++) {
              if (visible(cards[i])) { return false; }
            }
            return true;");

    /// <summary>
    /// What the dialog looks like right now, in one line, for a failure
    /// message. A test that says only that something was not visible
    /// leaves the next person to open a browser by hand and find out why.
    /// </summary>
    public string PlatformState()
        => Script<string>(@"
            var root = pmpRoot();
            if (!root) { return 'the platform has rendered nothing'; }
            function describe(el, name) {
              if (!el) { return name + '=absent'; }
              var box = el.getBoundingClientRect();
              var style = window.getComputedStyle(el);
              return name + '=' + (visible(el) ? 'visible' : 'hidden')
                + '(' + Math.round(box.width) + 'x' + Math.round(box.height)
                + ' display:' + style.display
                + ' visibility:' + style.visibility
                + ' opacity:' + style.opacity + ')';
            }
            var parts = [];
            var container = root.querySelector('.pmp');
            parts.push('container class=' + (container
              ? container.className : 'absent'));
            parts.push(describe(
              root.querySelector('[data-action=""open""]'), 'bubble'));
            var cards = root.querySelectorAll('[data-card]');
            for (var i = 0; i < cards.length; i++) {
              parts.push(describe(
                cards[i], 'card:' + cards[i].getAttribute('data-card')));
            }
            return parts.join(', ');") ?? "unreadable";

    /// <summary>Presses one of the dialog's buttons.</summary>
    public void Press(string action)
    {
        var pressed = Script<bool>($@"
            var root = pmpRoot();
            if (!root) {{ return false; }}
            var el = root.querySelector('[data-action=""{action}""]');
            if (!el) {{ return false; }}
            el.click();
            return true;");
        Assert.IsTrue(
            pressed,
            $"'{action}' was not on the page to press in {BrowserName}. "
            + $"The console said: {string.Join(" | ", Console())}");
    }

    /// <summary>
    /// The answer the platform is holding, through the getter it exposes
    /// for a publisher. Empty where it holds none.
    /// </summary>
    public string PlatformPreference()
        => Script<string>(@"
            var api = window.__51d_pmp;
            if (!api || typeof api.preference !== 'function') { return ''; }
            var value = api.preference();
            return value === undefined || value === null ? '' : String(value);")
            ?? "";

    /// <summary>Opens the dialog again, as a publisher's own link would.</summary>
    public void OpenPlatform()
        => Script<object>("window.__51d_pmp.open(); return null;");

    /// <summary>
    /// What the framework surface answers a ping with, being whether the
    /// regulation is said to apply.
    /// </summary>
    public bool GdprApplies()
    {
        Script<object>(@"
            window.__51dPing = null;
            window.__tcfapi('ping', 2, function (result) {
              window.__51dPing = result;
            });
            return null;");
        Harness.Until(
            () => Script<bool>("return window.__51dPing !== null;"),
            "the framework surface answered a ping");
        return Script<bool>("return !!window.__51dPing.gdprApplies;");
    }

    #endregion

    #region Waiting

    /// <summary>
    /// Waits until the client script has finished the given number of
    /// rounds. A round is one request to the cloud and the response to it,
    /// so this is an ordering and not a clock.
    /// </summary>
    public void WaitForClientRounds(int rounds)
        => Harness.Until(
            () => ClientRequests().Count(r => r.Done) >= rounds,
            $"the client script finished {rounds} round(s) in "
            + $"{BrowserName}. It finished "
            + $"{ClientRequests().Count(r => r.Done)}. {ServedSoFar()}. "
            + $"The console said: {string.Join(" | ", Console())}");

    /// <summary>
    /// What the browser asked the publisher's website for, which is the
    /// one thing that separates a page that never loaded from a page that
    /// loaded and then did nothing.
    /// </summary>
    public string ServedSoFar()
        => Server.Requests.Count == 0
            ? "the browser asked this website for nothing at all"
            : "the website served " + string.Join(", ", Server.Requests);

    /// <summary>Waits until the platform has put its dialog on the page.</summary>
    public void WaitForPlatform()
        => Harness.Until(
            PlatformLoaded,
            $"the preference platform loaded in {BrowserName}. "
            + $"{ServedSoFar()}. The console said: "
            + $"{string.Join(" | ", Console())}");

    /// <summary>Waits until a named card is showing.</summary>
    public void WaitForCard(string card)
        => Harness.Until(
            () => CardVisible(card),
            $"the {card} card appeared in {BrowserName}. "
            + $"{ServedSoFar()}. The console said: "
            + $"{string.Join(" | ", Console())}");

    /// <summary>Waits until the client script's object exists.</summary>
    public void WaitForClientObject(
        string objectName = Pages.DefaultObjectName)
        => Harness.Until(
            () => HasClientObject(objectName),
            $"an object called '{objectName}' appeared in {BrowserName}. "
            + $"{ServedSoFar()}. The console said: "
            + $"{string.Join(" | ", Console())}");

    #endregion

    public void Dispose()
    {
        try
        {
            Driver.Quit();
        }
        catch (Exception)
        {
            // A browser that has already gone is not a test failure.
        }
        Driver.Dispose();
    }
}
