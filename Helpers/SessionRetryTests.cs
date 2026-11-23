using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Tests;

/// <summary>
/// Checks that a browser session which fails to start quickly is started
/// again, and that one which hangs is not. Nothing here starts a browser or
/// waits, so these run anywhere and at once.
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
    /// The message Selenium gives when a start hangs until the session's
    /// time limit, reported as a WebDriverException.
    /// </summary>
    private const string HungMessage =
        "The HTTP request to the remote WebDriver server for URL "
        + "http://localhost:9515/session timed out after 300 seconds.";

    /// <summary>
    /// Seconds the renderer failure takes to answer, as measured on the
    /// Windows runners.
    /// </summary>
    private const int RendererFailureSeconds = 1;

    /// <summary>
    /// The most seconds the attempts may add to the longest one start can
    /// take, so a browser that will not start fails its test rather than
    /// holding the run.
    /// </summary>
    private const int MostSecondsTheAttemptsMayAdd = 120;

    private static readonly TimeSpan _rendererFailure =
        TimeSpan.FromSeconds(RendererFailureSeconds);

    private static readonly TimeSpan _sessionTimeout =
        TimeSpan.FromSeconds(BrowserDrivers.SessionTimeoutSeconds);

    private static readonly TimeSpan _retryWithin =
        TimeSpan.FromSeconds(BrowserDrivers.SessionRetryWithinSeconds);

    /// <summary>
    /// A clock that moves only when an attempt or a wait moves it, and
    /// records each wait.
    /// </summary>
    private sealed class FakeClock
    {
        public TimeSpan Now { get; private set; }

        public List<TimeSpan> Waits { get; } = new List<TimeSpan>();

        public void Pass(TimeSpan time) => Now += time;

        public void Wait(TimeSpan time)
        {
            Waits.Add(time);
            Now += time;
        }

        public T Retried<T>(Func<T> create) =>
            BrowserDrivers.Retried("Chrome", create, () => Now, Wait);
    }

    /// <summary>
    /// A session that starts is handed back and started once.
    /// </summary>
    [TestMethod]
    public void Retried_ReturnsWhatTheFirstAttemptGives()
    {
        var clock = new FakeClock();
        var attempts = 0;
        var session = clock.Retried(() =>
        {
            attempts++;
            return "a session";
        });
        Assert.AreEqual("a session", session);
        Assert.AreEqual(1, attempts);
        Assert.AreEqual(0, clock.Waits.Count);
    }

    /// <summary>
    /// A session the driver could not create is started again after the
    /// wait, which is the renderer failure seen on the Windows runners.
    /// </summary>
    [TestMethod]
    public void Retried_StartsAgainWhenTheSessionWasNotCreated()
    {
        var clock = new FakeClock();
        var attempts = 0;
        var session = clock.Retried(() =>
        {
            attempts++;
            if (attempts == 1)
            {
                clock.Pass(_rendererFailure);
                throw new InvalidOperationException(RendererMessage);
            }
            return "a session";
        });
        Assert.AreEqual("a session", session);
        Assert.AreEqual(2, attempts);
        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromSeconds(
                BrowserDrivers.SessionRetryDelaySeconds) },
            clock.Waits);
    }

    /// <summary>
    /// A driver service that would not run is started again. Selenium
    /// reports that one as a WebDriverException rather than an
    /// InvalidOperationException.
    /// </summary>
    [TestMethod]
    public void Retried_StartsAgainWhenTheDriverServiceWouldNotRun()
    {
        var clock = new FakeClock();
        var attempts = 0;
        var session = clock.Retried(() =>
        {
            attempts++;
            if (attempts == 1)
            {
                clock.Pass(_rendererFailure);
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
        var clock = new FakeClock();
        var attempts = 0;
        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => clock.Retried<string>(() =>
            {
                attempts++;
                clock.Pass(_rendererFailure);
                throw new InvalidOperationException(
                    RendererMessage + " on attempt " + attempts);
            }));
        Assert.AreEqual(BrowserDrivers.SessionStartAttempts, attempts);
        Assert.AreEqual(
            BrowserDrivers.SessionStartAttempts - 1, clock.Waits.Count);
        Assert.AreEqual(
            RendererMessage + " on attempt "
            + BrowserDrivers.SessionStartAttempts,
            failure.Message);
    }

    /// <summary>
    /// A start that hangs until the session's time limit is not made again,
    /// so a browser that hangs holds a test no longer than it did with one
    /// attempt.
    /// </summary>
    [TestMethod]
    public void Retried_DoesNotStartAgainWhenAStartHangs()
    {
        var clock = new FakeClock();
        var attempts = 0;
        var failure = Assert.ThrowsExactly<WebDriverException>(
            () => clock.Retried<string>(() =>
            {
                attempts++;
                clock.Pass(_sessionTimeout);
                throw new WebDriverException(HungMessage);
            }));
        Assert.AreEqual(1, attempts);
        Assert.AreEqual(0, clock.Waits.Count);
        Assert.AreEqual(_sessionTimeout, clock.Now);
        Assert.AreEqual(HungMessage, failure.Message);
    }

    /// <summary>
    /// An attempt that took the whole of
    /// <see cref="BrowserDrivers.SessionRetryWithinSeconds"/> to fail is not
    /// made again, and one that failed just inside it is.
    /// </summary>
    [TestMethod]
    public void Retried_StartsAgainOnlyWhenTheFailureWasQuick()
    {
        var slow = new FakeClock();
        var slowAttempts = 0;
        Assert.ThrowsExactly<InvalidOperationException>(
            () => slow.Retried<string>(() =>
            {
                slowAttempts++;
                slow.Pass(_retryWithin);
                throw new InvalidOperationException(RendererMessage);
            }));
        Assert.AreEqual(1, slowAttempts);

        var quick = new FakeClock();
        var quickAttempts = 0;
        quick.Retried(() =>
        {
            quickAttempts++;
            if (quickAttempts == 1)
            {
                quick.Pass(_retryWithin - TimeSpan.FromTicks(1));
                throw new InvalidOperationException(RendererMessage);
            }
            return "a session";
        });
        Assert.AreEqual(2, quickAttempts);
    }

    /// <summary>
    /// The attempts add less than <see cref="MostSecondsTheAttemptsMayAdd"/>
    /// to the longest one start can take, both when every attempt hangs
    /// until the session's time limit and when every attempt but the last
    /// fails just inside the quick limit and the last hangs.
    /// </summary>
    /// <param name="earlierAttemptsHang">
    /// True when the attempts before the last hang as the last does.
    /// </param>
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Retried_AddsLittleToTheLongestStart(bool earlierAttemptsHang)
    {
        Assert.IsTrue(
            BrowserDrivers.SessionStartAttempts > 1,
            "A session must be allowed more than one attempt, or nothing "
            + "is started again.");
        var clock = new FakeClock();
        var attempts = 0;
        Assert.ThrowsExactly<WebDriverException>(
            () => clock.Retried<string>(() =>
            {
                attempts++;
                if (earlierAttemptsHang == false
                    && attempts < BrowserDrivers.SessionStartAttempts)
                {
                    clock.Pass(_retryWithin - TimeSpan.FromTicks(1));
                    throw new InvalidOperationException(RendererMessage);
                }
                clock.Pass(_sessionTimeout);
                throw new WebDriverException(HungMessage);
            }));
        var added = clock.Now - _sessionTimeout;
        Assert.IsTrue(
            added < TimeSpan.FromSeconds(MostSecondsTheAttemptsMayAdd),
            $"The attempts added {added.TotalSeconds} s to the longest one "
            + $"start can take, where at most {MostSecondsTheAttemptsMayAdd} "
            + "s is allowed.");
    }

    /// <summary>
    /// A fault in the test itself, rather than in starting the browser, is
    /// reported at once and not started again.
    /// </summary>
    [TestMethod]
    public void Retried_DoesNotStartAgainAfterAFaultInTheCaller()
    {
        var clock = new FakeClock();
        var attempts = 0;
        Assert.ThrowsExactly<ArgumentException>(
            () => clock.Retried<string>(() =>
            {
                attempts++;
                throw new ArgumentException("the options are wrong");
            }));
        Assert.AreEqual(1, attempts);
    }

    /// <summary>
    /// Options the driver rejects, and a driver that cannot be found, are
    /// reported at once, though Selenium raises each as a
    /// WebDriverException, because another attempt cannot change them.
    /// </summary>
    /// <param name="type">The exception Selenium raises.</param>
    [TestMethod]
    [DataRow(typeof(WebDriverArgumentException))]
    [DataRow(typeof(NoSuchDriverException))]
    [DataRow(typeof(DriverServiceNotFoundException))]
    public void Retried_DoesNotStartAgainWhenAnotherAttemptCannotHelp(
        Type type)
    {
        var clock = new FakeClock();
        var attempts = 0;
        var failure = Assert.Throws<WebDriverException>(
            () => clock.Retried<string>(() =>
            {
                attempts++;
                clock.Pass(_rendererFailure);
                throw (Exception)Activator.CreateInstance(type, "rejected");
            }));
        Assert.AreEqual(1, attempts);
        Assert.AreEqual(0, clock.Waits.Count);
        Assert.AreEqual(type, failure.GetType());
    }
}
