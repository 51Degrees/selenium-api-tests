using System;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Tests;

/// <summary>
/// Checks that a browser session which fails to start is started again.
/// Nothing here starts a browser, so these run anywhere.
/// </summary>
[TestClass]
public class SessionRetryTests
{
    /// <summary>
    /// The message Chrome's driver gives when the browser process starts but
    /// its renderer never answers, which is the failure these tests exist
    /// for. Selenium reports it as an InvalidOperationException.
    /// </summary>
    private const string RendererMessage =
        "session not created from disconnected: unable to connect to "
        + "renderer (SessionNotCreated)";

    /// <summary>
    /// A session that starts is handed back and started once.
    /// </summary>
    [TestMethod]
    public void Retried_ReturnsWhatTheFirstAttemptGives()
    {
        var attempts = 0;
        var session = BrowserDrivers.Retried("Chrome", () =>
        {
            attempts++;
            return "a session";
        });
        Assert.AreEqual("a session", session);
        Assert.AreEqual(1, attempts);
    }

    /// <summary>
    /// A session the driver could not create is started again, which is the
    /// renderer failure seen on the Windows runners.
    /// </summary>
    [TestMethod]
    public void Retried_StartsAgainWhenTheSessionWasNotCreated()
    {
        var attempts = 0;
        var session = BrowserDrivers.Retried("Chrome", () =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException(RendererMessage);
            }
            return "a session";
        });
        Assert.AreEqual("a session", session);
        Assert.AreEqual(2, attempts);
    }

    /// <summary>
    /// A driver service that would not run is started again. Selenium
    /// reports that one as a WebDriverException rather than an
    /// InvalidOperationException.
    /// </summary>
    [TestMethod]
    public void Retried_StartsAgainWhenTheDriverServiceWouldNotRun()
    {
        var attempts = 0;
        var session = BrowserDrivers.Retried("Firefox", () =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new WebDriverException(
                    "Driver service process exited unexpectedly before "
                    + "initialization completed");
            }
            return "a session";
        });
        Assert.AreEqual("a session", session);
        Assert.AreEqual(2, attempts);
    }

    /// <summary>
    /// Every attempt is made, and the failure of the last one is thrown as
    /// it stands, so a test reports the same error it reports without this.
    /// </summary>
    [TestMethod]
    public void Retried_ThrowsTheLastFailureWhenNoAttemptStarts()
    {
        var attempts = 0;
        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => BrowserDrivers.Retried<string>("Chrome", () =>
            {
                attempts++;
                throw new InvalidOperationException(
                    RendererMessage + " on attempt " + attempts);
            }));
        Assert.AreEqual(BrowserDrivers.SessionStartAttempts, attempts);
        Assert.AreEqual(
            RendererMessage + " on attempt "
            + BrowserDrivers.SessionStartAttempts,
            failure.Message);
    }

    /// <summary>
    /// A fault in the test itself, rather than in starting the browser, is
    /// reported at once and not started again.
    /// </summary>
    [TestMethod]
    public void Retried_DoesNotStartAgainAfterAFaultInTheCaller()
    {
        var attempts = 0;
        Assert.ThrowsExactly<ArgumentException>(
            () => BrowserDrivers.Retried<string>("Chrome", () =>
            {
                attempts++;
                throw new ArgumentException("the options are wrong");
            }));
        Assert.AreEqual(1, attempts);
    }

    /// <summary>
    /// More than one attempt is allowed, and the wait between them is short
    /// enough that a browser that will not start does not hold the run.
    /// </summary>
    [TestMethod]
    public void SessionStartAttempts_AllowsMoreThanOneAttempt()
    {
        Assert.IsTrue(
            BrowserDrivers.SessionStartAttempts > 1,
            "A session must be allowed more than one attempt, or nothing "
            + "is started again.");
        Assert.IsTrue(
            BrowserDrivers.SessionStartAttempts
            * BrowserDrivers.SessionRetryDelaySeconds < 30,
            "The attempts and the wait between them must add up to less "
            + "than 30 s, so a browser that will not start fails the test "
            + "rather than holding the run.");
    }
}
