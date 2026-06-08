using OpenQA.Selenium;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;

/// <summary>
/// Reads client-side evidence captured by the 51Degrees JS bundle.
/// </summary>
public static class JsEvidenceHelper
{
    /// <summary>
    /// Returns the value the bundle stored for the given evidence key, looking
    /// first in cookies and then in sessionStorage. Null if it isn't present.
    /// </summary>
    public static long? Read(IJavaScriptExecutor js, WebDriver driver, string key)
    {
        var cookie = driver.Manage().Cookies.GetCookieNamed(key);
        if (cookie != null && long.TryParse(cookie.Value, out var cookieValue))
        {
            return cookieValue;
        }
        // the bundle also stores evidence in sessionStorage as a JSON blob
        var script = $@"
            for (var i = 0; i < sessionStorage.length; i++) {{
                var k = sessionStorage.key(i);
                if (k && k.endsWith('_parameters')) {{
                    try {{
                        var p = JSON.parse(sessionStorage.getItem(k));
                        if (p && p['{key}'] !== undefined) return p['{key}'];
                    }} catch (e) {{}}
                }}
            }}
            return null;";
        var raw = js.ExecuteScript(script)?.ToString();
        return long.TryParse(raw, out var sessionValue) ? sessionValue : (long?)null;
    }
}
