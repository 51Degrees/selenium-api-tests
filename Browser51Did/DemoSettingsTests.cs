#nullable enable

using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.Cloud.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// Which of its two names the demo's resource key is read from. The rules
/// are read through a lookup the test supplies, so nothing here changes the
/// environment of the run around it.
/// <para>
/// These exist because the key was once read straight from the process
/// environment while every other setting went through the lookup a
/// <see cref="TestConfig"/> is given, so a configuration built for a test
/// quietly ignored what the test gave it for this one setting.
/// </para>
/// </summary>
[TestClass, TestCategory("Browser51Did")]
public class DemoSettingsTests
{
    private const string RuntimeKey = "NOTAKEYFROMTHERUNTIMENAME";
    private const string CiKey = "NOTAKEYFROMTHECINAME";

    private static TestConfig Config(params string[] namesAndValues)
    {
        var values = new Dictionary<string, string>();
        for (var index = 0; index < namesAndValues.Length; index += 2)
        {
            values[namesAndValues[index]] = namesAndValues[index + 1];
        }
        return new TestConfig(
            name => values.TryGetValue(name, out var value) ? value : null!);
    }

    /// <summary>
    /// A developer's own key wins over the one continuous integration set,
    /// because the runtime name is the one every language's demo reads.
    /// </summary>
    [TestMethod]
    public void BothNamesSet_TheRuntimeNameWins()
    {
        var config = Config(
            TestConfig.DemoResourceKeyVariable, RuntimeKey,
            TestConfig.DemoResourceKeyCiVariable, CiKey);
        Assert.AreEqual(RuntimeKey, config.DemoResourceKey);
    }

    /// <summary>
    /// Where only continuous integration's name is set, that is used, and
    /// an empty runtime name counts as unset.
    /// </summary>
    [TestMethod]
    public void OnlyTheCiNameSet_ItIsUsed()
    {
        var config = Config(
            TestConfig.DemoResourceKeyVariable, "",
            TestConfig.DemoResourceKeyCiVariable, CiKey);
        Assert.AreEqual(CiKey, config.DemoResourceKey);
    }

    /// <summary>
    /// Neither set fails naming both, in the order they are read, so the
    /// reader knows either will do.
    /// </summary>
    [TestMethod]
    public void NeitherNameSet_TheFailureNamesBoth()
    {
        var config = Config();
        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = config.DemoResourceKey);
        StringAssert.Contains(
            failure.Message,
            $"'{TestConfig.DemoResourceKeyVariable}', or where that is "
            + $"unset '{TestConfig.DemoResourceKeyCiVariable}'");
    }
}
