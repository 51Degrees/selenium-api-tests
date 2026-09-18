#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// That a resource key cannot reach a test report. These tests need no
/// cloud, no demo and no browser, because what they check is the suite's
/// own handling of a result, so they run wherever the category runs.
/// <para>
/// They exist because the redaction was once done by editing the result
/// MSTest had already built. That looked right and was not: assigning
/// <see cref="TestResult.TestFailureException"/> a second time does not
/// replace the first exception, it aggregates with it, and the first one's
/// message has already been copied into a member of MSTest's own that this
/// assembly cannot set. The runner therefore reported the key and the
/// redaction of it side by side. The fix is to report a result built here
/// instead, which has neither. These tests fail against the edit-in-place
/// version and pass against the copy.
/// </para>
/// </summary>
[TestClass, TestCategory("Browser51Did")]
public class RedactionTests
{
    /// <summary>
    /// A resource key that is not one, long enough that it cannot appear
    /// in a message by accident.
    /// </summary>
    private const string Key = "NOTAREALRESOURCEKEY0123456789";

    /// <summary>
    /// Text of the shape a failure here really takes: the client script's
    /// address, which names the resource key.
    /// </summary>
    private const string Mentions =
        "the script at https://cloud.example/" + Key + ".js was wrong";

    /// <summary>
    /// A result with the key in every piece of text it holds, standing in
    /// for the one MSTest hands the attribute after a test has failed.
    /// Every piece is set by reflection rather than by name, so a member
    /// added by a later MSTest is covered without this test being changed.
    /// </summary>
    private static TestResult ResultMentioningTheKeyThroughout()
    {
        var result = new TestResult
        {
            Outcome = UnitTestOutcome.Failed,
            TestFailureException = new AssertFailedException(Mentions),
        };
        foreach (var field in TextFields())
        {
            field.SetValue(result, Mentions);
        }
        return result;
    }

    private static IEnumerable<FieldInfo> TextFields()
    {
        foreach (var field in typeof(TestResult).GetFields(
            BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(string))
            {
                yield return field;
            }
        }
    }

    /// <summary>
    /// The one that would have caught the bug. Nothing the runner can read
    /// off the result may carry the key, including the members the public
    /// interface does not name.
    /// </summary>
    [TestMethod]
    public void NoTextOnTheReportedResultCarriesTheKey()
    {
        var reported = Browser51DidTestAttribute.Redacted(
            ResultMentioningTheKeyThroughout(), Key);
        foreach (var field in TextFields())
        {
            var text = (string?)field.GetValue(reported);
            Assert.IsFalse(
                text != null
                && text.Contains(Key, StringComparison.Ordinal),
                $"TestResult.{field.Name} still carries the resource key "
                + "after redaction, so a failing test would print it into "
                + "a public build log. Anything the result holds has to be "
                + "set from the redacted text or left unset.");
        }
        Assert.IsFalse(
            reported.TestFailureException!.ToString()
                .Contains(Key, StringComparison.Ordinal),
            "the failure the runner reports still carries the resource "
            + "key.");
    }

    /// <summary>
    /// Why the test above cannot be satisfied by editing the result in
    /// place: MSTest holds the failure text somewhere this assembly cannot
    /// name, so there is more to clear than the four public ones. If this
    /// ever fails, MSTest has stopped keeping that copy and the copying in
    /// <see cref="Browser51DidTestAttribute"/> can be reconsidered.
    /// </summary>
    [TestMethod]
    public void MSTestKeepsTextBesideTheMembersThisSuiteCanSet()
    {
        var named = new[]
        {
            nameof(TestResult.DisplayName),
            nameof(TestResult.LogOutput),
            nameof(TestResult.LogError),
            nameof(TestResult.DebugTrace),
            nameof(TestResult.TestContextMessages),
        };
        var unnamed = 0;
        foreach (var field in TextFields())
        {
            if (Array.Exists(
                named,
                name => field.Name.Contains(name, StringComparison.Ordinal))
                == false)
            {
                unnamed++;
            }
        }
        Assert.IsTrue(
            unnamed > 0,
            "TestResult no longer holds text outside the members this "
            + "suite sets by name, so the reason for copying the result "
            + "rather than editing it has gone.");
    }

    /// <summary>
    /// The redaction has to leave the failure readable, or a build log says
    /// only that something went wrong.
    /// </summary>
    [TestMethod]
    public void TheRedactedFailureStillSaysWhatWentWrong()
    {
        var reported = Browser51DidTestAttribute.Redacted(
            ResultMentioningTheKeyThroughout(), Key);
        var message = reported.TestFailureException!.ToString();
        StringAssert.Contains(
            message,
            "the script at https://cloud.example/<resource key>.js",
            "the address that failed must still be readable with the key "
            + "taken out of it.");
    }

    /// <summary>
    /// A skip is a result too, and its reason reaches the same log.
    /// </summary>
    [TestMethod]
    public void AnInconclusiveResultStaysInconclusiveAndIsRedacted()
    {
        var skipped = new TestResult
        {
            Outcome = UnitTestOutcome.Inconclusive,
            TestFailureException = new AssertInconclusiveException(Mentions),
        };
        var reported = Browser51DidTestAttribute.Redacted(skipped, Key);
        Assert.AreEqual(
            UnitTestOutcome.Inconclusive,
            reported.Outcome,
            "a skip must still report as a skip.");
        Assert.IsInstanceOfType<AssertInconclusiveException>(
            reported.TestFailureException,
            "a skip reported through a failure exception is counted as a "
            + "failure.");
        Assert.IsFalse(
            reported.TestFailureException!.ToString()
                .Contains(Key, StringComparison.Ordinal),
            "the skip reason still carries the resource key.");
    }

    /// <summary>
    /// The copy must not quietly drop what the runner uses to place a
    /// result, which is what makes a data row's result its own.
    /// </summary>
    [TestMethod]
    public void TheCopyKeepsWhatIdentifiesTheResult()
    {
        var original = new TestResult
        {
            DisplayName = "a name with no key in it",
            Outcome = UnitTestOutcome.Passed,
            Duration = TimeSpan.FromSeconds(3),
            ExecutionId = Guid.NewGuid(),
            ParentExecId = Guid.NewGuid(),
            ResultFiles = new List<string> { "screenshot.png" },
        };
        var reported = Browser51DidTestAttribute.Redacted(original, Key);
        Assert.AreEqual(original.DisplayName, reported.DisplayName);
        Assert.AreEqual(original.Outcome, reported.Outcome);
        Assert.AreEqual(original.Duration, reported.Duration);
        Assert.AreEqual(original.ExecutionId, reported.ExecutionId);
        Assert.AreEqual(original.ParentExecId, reported.ParentExecId);
        CollectionAssert.AreEqual(
            (List<string>)original.ResultFiles!,
            (List<string>?)reported.ResultFiles,
            "a result file the test left behind must still be reported.");
    }

    /// <summary>
    /// The guard prints a slice of the cloud-rendered client script to show
    /// which template it was built from. That script calls back on
    /// addresses naming the resource key, so a slice of it can contain the
    /// key wherever the marker happens to fall.
    /// </summary>
    [TestMethod]
    public void TheGuardsEvidenceSliceIsRedacted()
    {
        var marker = "fod.complete";
        var body =
            "var url = 'https://cloud.example/" + Key + ".json';\n"
            + "// " + marker + " is what says the script finished\n"
            + "function done() { " + marker + "(); }";
        var at = body.IndexOf(marker, StringComparison.Ordinal);
        var evidence = Harness.Around(body, at, Key);
        StringAssert.Contains(
            evidence,
            marker,
            "the slice must still show the marker it was taken around.");
        Assert.IsFalse(
            evidence.Contains(Key, StringComparison.Ordinal),
            "the guard's evidence still carries the resource key. It is "
            + "written to the console from ClassInitialize, outside "
            + "Browser51DidTestAttribute, so nothing else takes it out.");
    }
}
