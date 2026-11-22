#nullable enable

using System;
using System.Collections.Generic;
using FiftyOne.Pipeline.Cloud.Tests.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// Where the demo's resource key and cloud endpoint are read from. Each is
/// read under one name. The rules are read through a lookup the test
/// supplies, so nothing here changes the environment of the run around it.
/// </summary>
[TestClass, TestCategory("Browser51Did")]
public class DemoSettingsTests
{
    private const string Key = "NOTAKEYFROMTHE51DIDNAME";
    private const string Endpoint = "https://cloud.example.test/api/v4/";

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
    /// The key is the one for the 51Did service.
    /// </summary>
    [TestMethod]
    public void TheKeyIsReadFromThe51DidName()
    {
        var config = Config(TestConfig.DemoResourceKeyVariable, Key);
        Assert.AreEqual(Key, config.DemoResourceKey);
    }

    /// <summary>
    /// No other key name stands in for it.
    /// </summary>
    [TestMethod]
    public void AnotherKeyName_IsNotRead()
    {
        var config = Config("_51DEGREES_RESOURCE_KEY", Key);
        var failure = Assert.ThrowsExactly<InvalidOperationException>(
            () => _ = config.DemoResourceKey);
        StringAssert.Contains(
            failure.Message, $"'{TestConfig.DemoResourceKeyVariable}'");
        Assert.IsFalse(
            failure.Message.Contains(Key, StringComparison.Ordinal),
            "the message names the variable and never a value");
    }

    /// <summary>
    /// The endpoint is read as it is set, with its api/v4 path.
    /// </summary>
    [TestMethod]
    public void TheEndpointIsReadAsItIsSet()
    {
        var config = Config(TestConfig.CloudEndpointVariable, Endpoint);
        Assert.AreEqual(Endpoint, config.DemoCloudEndpoint);
    }

    /// <summary>
    /// With no endpoint set, the demo uses the public cloud service.
    /// </summary>
    [TestMethod]
    public void NoEndpointSet_ThePublicServiceIsUsed()
    {
        var config = Config();
        Assert.AreEqual(
            "https://cloud.51degrees.com/api/v4/", config.DemoCloudEndpoint);
    }
}
