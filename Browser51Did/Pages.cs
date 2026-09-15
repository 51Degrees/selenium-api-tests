#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// The real pages a browser loads in these tests, being a publisher's page
/// with the preference platform and the client script on it in one
/// arrangement or another.
/// <para>
/// Every page starts with the recorder, which is the only thing on any of
/// them that is not a publisher's own markup. It wraps the two ways a page
/// makes a request and the console, so a test can read what the page
/// actually sent, what came back and what was logged, without a proxy in
/// front of the cloud. A proxy would put the cloud on the page's own
/// origin, and the whole first demonstration is about the cloud being a
/// third party to the page.
/// </para>
/// <para>
/// Nothing on these pages is publisher code in the sense the design means
/// it. The recorder is the test's instrument, and where a page carries a
/// handler, the test that uses that page says why.
/// </para>
/// </summary>
public static class Pages
{
    /// <summary>
    /// The object name a page uses when it does not ask for another one,
    /// which is what the client script and the preference platform both
    /// fall back to.
    /// </summary>
    public const string DefaultObjectName = "fod";

    /// <summary>
    /// A publisher's static vendor consent string, as the platform's own
    /// demo page carries one. The platform only rewrites the purpose
    /// consent bits and the dates in it.
    /// </summary>
    public const string PublisherVendorString =
        "CPYBSvoPYBSvoO3AAAENAwCAAAAAAAAAAAAAAAAAAAAA";

    /// <summary>
    /// The recorder. It is first on every page, before anything is loaded
    /// from the cloud, so nothing the page does afterwards escapes it.
    /// </summary>
    public const string Recorder = @"
<script>
(function () {
  var t = window.__51dTest = {
    requests: [],
    console: [],
    actions: [],
    changes: [],
    errors: [],
    altFired: false
  };
  function text(value) {
    if (typeof value === 'string') { return value; }
    try { return JSON.stringify(value); } catch (e) { return String(value); }
  }
  function record(kind, method, url, body) {
    var entry = {
      kind: kind,
      method: String(method || 'GET').toUpperCase(),
      url: String(url || ''),
      body: typeof body === 'string' ? body : '',
      status: 0,
      response: '',
      done: false
    };
    t.requests.push(entry);
    return entry;
  }
  var open = XMLHttpRequest.prototype.open;
  XMLHttpRequest.prototype.open = function (method, url) {
    this.__51dCall = { method: method, url: url };
    return open.apply(this, arguments);
  };
  var send = XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.send = function (body) {
    var call = this.__51dCall || {};
    var entry = record('xhr', call.method, call.url, body);
    var xhr = this;
    this.addEventListener('loadend', function () {
      entry.status = xhr.status;
      try { entry.response = xhr.responseText || ''; }
      catch (e) { entry.response = ''; }
      entry.done = true;
    });
    return send.apply(this, arguments);
  };
  if (window.fetch) {
    var realFetch = window.fetch.bind(window);
    window.fetch = function (input, init) {
      var url = typeof input === 'string'
        ? input : (input && input.url) || '';
      var method = (init && init.method)
        || (input && input.method) || 'GET';
      var body = init && typeof init.body === 'string' ? init.body : '';
      var entry = record('fetch', method, url, body);
      return realFetch(input, init).then(function (response) {
        entry.status = response.status;
        entry.done = true;
        try {
          response.clone().text().then(function (value) {
            entry.response = value;
          }, function () { });
        } catch (e) { }
        return response;
      }, function (error) {
        entry.error = String(error);
        entry.done = true;
        throw error;
      });
    };
  }
  var levels = ['log', 'info', 'warn', 'error', 'debug'];
  for (var i = 0; i < levels.length; i++) {
    (function (level) {
      var real = console[level];
      console[level] = function () {
        var parts = [];
        for (var a = 0; a < arguments.length; a++) {
          parts.push(text(arguments[a]));
        }
        t.console.push({ level: level, text: parts.join(' ') });
        if (real) { real.apply(console, arguments); }
      };
    })(levels[i]);
  }
  window.addEventListener('error', function (e) {
    t.errors.push(String(e && e.message));
  });
})();
</script>";

    /// <summary>
    /// A handler registered on the client script's object as soon as it
    /// exists, so a test can assert that a change of answer reached page
    /// code. It waits for the object rather than assuming the script has
    /// finished, because the script tag is asynchronous.
    /// <para>
    /// This is the one piece of publisher code any of these pages carries,
    /// and only the two tests about a change of answer use it.
    /// </para>
    /// </summary>
    public static string ChangeWatcher(string objectName) => $@"
<script>
(function () {{
  var waiting = setInterval(function () {{
    var o = window['{objectName}'];
    if (!o || typeof o.onChange !== 'function') {{ return; }}
    clearInterval(waiting);
    o.onChange(function (data) {{
      window.__51dTest.changes.push(
        data && data.fodid ? data.fodid : null);
    }});
  }}, 20);
}})();
</script>";

    /// <summary>
    /// The client script's own tag, exactly as a publisher writes it.
    /// </summary>
    public static string ClientScriptTag(string? objectName = null)
        => $"<script async src=\"{Harness.ClientScriptUrl(objectName)}\">"
            + "</script>";

    /// <summary>
    /// Settings on the preference platform's tag. Every one of them is an
    /// attribute the publisher writes, and the defaults here are the ones
    /// the platform's own demo page uses.
    /// </summary>
    public sealed class PlatformSettings
    {
        /// <summary>The network a choice is shared across. Null turns
        /// sharing off, because the visitor cannot be asked to share with
        /// a group nobody has named.</summary>
        public string? NetworkName { get; set; } = Harness.NetworkName;

        /// <summary>Whether the standard answer is offered as well as the
        /// personalized one.</summary>
        public bool ShowStandard { get; set; } = true;

        /// <summary>The name the platform looks for the client script's
        /// object under. Null leaves the attribute off, which is the
        /// documented way of saying the default.</summary>
        public string? ObjectName { get; set; }

        /// <summary>How long the platform waits for the third party cookie
        /// answer before carrying on without one.</summary>
        public int TimeoutMs { get; set; } = 4500;
    }

    /// <summary>
    /// The preference platform's tag. The action and the alternative are
    /// page hooks rather than a second script, so a test can see them fire
    /// and so that nothing here loads a second copy of the client script,
    /// which two of the tests assert did not happen.
    /// </summary>
    public static string PlatformTag(PlatformSettings settings)
    {
        var attributes = new List<string>
        {
            $"data-resource-key=\"{Harness.Resource}\"",
            "data-action-url=\"javascript:window.__51dTest.actions"
                + ".push('{preference}')\"",
            $"data-tcf-vendor=\"{PublisherVendorString}\"",
            "data-brand-name=\"Fifty One Times\"",
            "data-brand-terms-url=\"https://example.com/privacy\"",
            "data-alt-name=\"Subscribe\"",
            "data-alt-url=\"javascript:window.__51dTest.altFired = true\"",
            $"data-show-standard=\"{(settings.ShowStandard ? "true" : "false")}\"",
            $"data-timeout=\"{settings.TimeoutMs}\"",
        };
        if (settings.NetworkName is null)
        {
            attributes.Add("data-use-third-party-cookies=\"false\"");
        }
        else
        {
            attributes.Add($"data-network-name=\"{settings.NetworkName}\"");
        }
        if (settings.ObjectName is not null)
        {
            attributes.Add($"data-object-name=\"{settings.ObjectName}\"");
        }
        return $"<script src=\"{Harness.PlatformLoaderUrl()}\"\n  "
            + string.Join("\n  ", attributes) + "></script>";
    }

    /// <summary>
    /// A consent platform, being the stub the framework's own
    /// specification tells every publisher to put in the page, and a fake
    /// platform behind it that answers only what the specification
    /// requires, which is ping, addEventListener and a callback carrying
    /// tcString and eventStatus.
    /// <para>
    /// It does not deliver anything until the test asks it to with
    /// <c>window.__51dCmp.deliver()</c>, so the test decides the ordering
    /// rather than a timer.
    /// </para>
    /// </summary>
    public static string ConsentPlatform(string tcString) => $@"
<script>
(function () {{
  // The stub from the framework's specification, verbatim in shape: it
  // queues calls until the platform itself is ready.
  var queue = [];
  window.__tcfapi = function () {{ queue.push(arguments); }};
  var listeners = [];
  var delivered = null;
  function ping(callback) {{
    callback({{
      gdprApplies: true,
      cmpLoaded: true,
      cmpStatus: 'loaded',
      displayStatus: 'hidden',
      apiVersion: '2.2'
    }}, true);
  }}
  function api(command, version, callback, parameter) {{
    if (command === 'ping') {{ ping(callback); return; }}
    if (command === 'addEventListener') {{
      listeners.push(callback);
      callback({{
        tcString: '',
        eventStatus: 'cmpuishown',
        gdprApplies: true,
        listenerId: listeners.length
      }}, true);
      if (delivered) {{ callback(delivered, true); }}
      return;
    }}
    if (command === 'removeEventListener') {{
      listeners[parameter - 1] = null;
      callback(true, true);
      return;
    }}
    callback(null, false);
  }}
  window.__51dCmp = {{
    deliver: function () {{
      delivered = {{
        tcString: '{tcString}',
        eventStatus: 'useractioncomplete',
        gdprApplies: true
      }};
      for (var i = 0; i < listeners.length; i++) {{
        if (listeners[i]) {{ listeners[i](delivered, true); }}
      }}
      return listeners.length;
    }},
    listeners: function () {{ return listeners.length; }}
  }};
  // The platform takes over from the stub and drains what queued.
  window.__tcfapi = api;
  for (var i = 0; i < queue.length; i++) {{
    api.apply(null, queue[i]);
  }}
}})();
</script>";

    /// <summary>Wraps the parts into a page.</summary>
    public static string Page(string title, params string[] parts)
    {
        var body = new StringBuilder();
        body.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n")
            .Append("<meta charset=\"utf-8\">\n")
            .Append("<title>").Append(title).Append("</title>\n")
            .Append(Recorder)
            .Append("\n</head>\n<body>\n<h1>")
            .Append(title)
            .Append("</h1>\n");
        foreach (var part in parts)
        {
            body.Append(part).Append('\n');
        }
        return body.Append("</body>\n</html>\n").ToString();
    }
}
