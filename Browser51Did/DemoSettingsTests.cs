#nullable enable

using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.Cloud.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// Which name the demo's resource key and cloud endpoint are read from.
/// The current names are read before the older ones, in the order the
/// demos read their own. The rules are read through a lookup the test
/// supplies, so nothing here changes the environment of the run around it.
/// </summary>
[TestClass, TestCategory("Browser51Did")]
public class DemoSettingsTests
{
    private const string RuntimeKey = "NOTAKEYFROMTHERUNTIMENAME";
    private const string CiKey = "NOTAKEYFROMTHECINAME";
    private const string OldKey = "NOTAKEYFROMTHEOLDNAME";
    private const string RuntimeEndpoint = "https://runtime.example.test/api/v4/";
    private const string OldEndpoint = "https://old.example.test/api/v4/";

    private readonly List<string> _reports = new List<string>();

    private TestConfig Config(params string[] namesAndValues)
    {
        var values = new Dictionary<string, string>();
        for (var index = 0; index < namesAndValues.Length; index += 2)
        {
            values[namesAndValues[index]] = namesAndValues[index + 1];
        }
        return new TestConfig(
            name => values.TryGetValue(name, out var value) ? value : null!,
            _reports.Add);
    }

    /// <summary>
    /// The runtime name wins over the one continuous integration sets and
    /// over the older name, because it is what a developer sets for this
    /// run, and nothing is reported.
    /// </summary>
    [TestMethod]
    public void AllThreeKeyNamesSet_TheRuntimeNameWins()
    {
        var config = Config(
            TestConfig.DemoResourceKeyVariable, RuntimeKey,
            TestConfig.DemoResourceKeyCiVariable, CiKey,
            TestConfig.DemoResourceKeyOldVariable, OldKey);
        Assert.AreEqual(RuntimeKey, config.DemoResourceKey);
        Assert.AreEqual(0, _reports.Count);
    }

    /// <summary>
    /// The CI name is a current name, so it is read before the older name,
    /// and an empty runtime name counts as unset.
    /// </summary>
    [TestMethod]
    public void CiAndOldKeyNamesSet_TheCiNameWins()
    {
        var config = Config(
            TestConfig.DemoResourceKeyVariable, "",
            TestConfig.DemoResourceKeyCiVariable, CiKey,
            TestConfig.DemoResourceKeyOldVariable, OldKey);
        Assert.AreEqual(CiKey, config.DemoResourceKey);
        Assert.AreEqual(0, _reports.Count);
    }

    /// <summary>
    /// Where only the older name is set it is still read, and one sentence
    /// says it is deprecated and names the one that replaces it.
    /// </summary>
    [TestMethod]
    public void OnlyTheOldKeyNameSet_ItIsReadAndReported()
    {
        var config = Config(TestConfig.DemoResourceKeyOldVariable, OldKey);
        Assert.AreEqual(OldKey, config.DemoResourceKey);
        Assert.AreEqual(1, _reports.Count);
        StringAssert.Contains(
            _reports[0],
            $"{TestConfig.DemoResourceKeyOldVariable} is deprecated and "
            + $"{TestConfig.DemoResourceKeyVariable} replaces it");
        Assert.IsFalse(
            _reports[0].Contains(OldKey, StringComparison.Ordinal),
            "the sentence names the variable and never its value");
    }

    /// <summary>
    /// No key name set fails naming all three, in the order they are read,
    /// so the reader knows any of them will do.
    /// </summary>
    [TestMethod]
    public void NoKeyNameSet_TheFailureNamesAllThree()
    {
        var config = Config();
        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = config.DemoResourceKey);
        StringAssert.Contains(
            failure.Message,
            $"'{TestConfig.DemoResourceKeyVariable}', or where that is "
            + $"unset '{TestConfig.DemoResourceKeyCiVariable}', or where "
            + $"that is unset '{TestConfig.DemoResourceKeyOldVariable}'");
    }

    /// <summary>
    /// The endpoint's runtime name wins over its older name.
    /// </summary>
    [TestMethod]
    public void BothEndpointNamesSet_TheRuntimeNameWins()
    {
        var config = Config(
            TestConfig.DemoCloudEndpointVariable, RuntimeEndpoint,
            TestConfig.DemoCloudEndpointOldVariable, OldEndpoint);
        Assert.AreEqual(RuntimeEndpoint, config.DemoCloudEndpoint);
        Assert.AreEqual(0, _reports.Count);
    }

    /// <summary>
    /// Where only the endpoint's older name is set it is still read, and
    /// reported in the same way as the key's.
    /// </summary>
    [TestMethod]
    public void OnlyTheOldEndpointNameSet_ItIsReadAndReported()
    {
        var config = Config(
            TestConfig.DemoCloudEndpointOldVariable, OldEndpoint);
        Assert.AreEqual(OldEndpoint, config.DemoCloudEndpoint);
        Assert.AreEqual(1, _reports.Count);
        StringAssert.Contains(
            _reports[0],
            $"{TestConfig.DemoCloudEndpointOldVariable} is deprecated and "
            + $"{TestConfig.DemoCloudEndpointVariable} replaces it");
    }

    /// <summary>
    /// No endpoint name set fails naming both, in the order they are read.
    /// </summary>
    [TestMethod]
    public void NoEndpointNameSet_TheFailureNamesBoth()
    {
        var config = Config();
        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = config.DemoCloudEndpoint);
        StringAssert.Contains(
            failure.Message,
            $"'{TestConfig.DemoCloudEndpointVariable}', or where that is "
            + $"unset '{TestConfig.DemoCloudEndpointOldVariable}'");
        Assert.AreEqual(0, _reports.Count);
    }
}
