using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;

/// <summary>
/// Helper class for external Selenium configuration.
/// </summary>
public static class ExternalSeleniumHelper
{
    /// <summary>
    /// Returns true and the SELENIUM_URL value when it is set in the environment.
    /// </summary>
    public static bool IsExternalSelenium(out string seleniumUrl)
    {
        seleniumUrl = Environment.GetEnvironmentVariable("SELENIUM_URL");
        return string.IsNullOrEmpty(seleniumUrl) == false;
    }
    
    /// <summary>
    /// Adds the driver arguments needed to run Selenium in a container.
    /// </summary>
    public static void AddExternalSeleniumArguments(DriverOptions  options)
    {
        const string noSandboxArgument = "--no-sandbox";
        const string disableDevShmUsageArgument = "--disable-dev-shm-usage";
        switch (options)
        {
            case ChromeOptions chromeOptions:
                chromeOptions.AddArgument(noSandboxArgument);
                chromeOptions.AddArgument(disableDevShmUsageArgument);
                break;
            case EdgeOptions  edgeOptions:
                edgeOptions.AddArgument(noSandboxArgument);
                edgeOptions.AddArgument(disableDevShmUsageArgument);
                break;
            case FirefoxOptions firefoxOptions:
                firefoxOptions.AddArgument(noSandboxArgument);
                firefoxOptions.AddArgument(disableDevShmUsageArgument);
                break;
            default:
                throw new ArgumentOutOfRangeException($"DriverOptions of type {options.GetType()} are not supported");
        }
        
    }
}
