#nullable enable

using System;
using System.Collections.Generic;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Browser51Did;

/// <summary>
/// The smallest framework string that says which purposes a visitor
/// granted. Only the purpose consent bits and the legitimate interest bits
/// are set, because those are the only ones the server reads when it works
/// out a usage from a string.
/// <para>
/// This is the same construction as <c>TcStringBuilder</c> in the cloud
/// repository's <c>Did/Tests/FiftyOne.Did.OnPremise.Tests</c>. It is
/// repeated here because that is another repository, and because a browser
/// test that quietly changed when a unit test helper changed would be worse
/// than a small repetition.
/// </para>
/// <para>
/// Every language's demo delivers <see cref="Personalized"/> from its stub
/// consent platform, which is
/// <c>AAAAAAAAAAAAAAAAAAAAAAAAAP_wAAAA</c>, and the consent test checks the
/// string that reached the cloud is exactly this.
/// </para>
/// </summary>
public static class TcString
{
    private const int ConsentStart = 152;
    private const int LegitimateInterestStart = 176;
    private const int PurposeCount = 12;

    /// <summary>
    /// Enough bytes for both purpose runs, which is the whole of the core
    /// segment the reader looks at.
    /// </summary>
    private const int Bytes = 24;

    /// <summary>
    /// The purposes the standard usage under the Model Terms for
    /// Marketing needs.
    /// </summary>
    public static readonly int[] StandardPurposes = { 1, 2, 7, 8, 11 };

    /// <summary>
    /// Every purpose, which is what the personalized usage needs.
    /// </summary>
    public static readonly int[] AllPurposes =
        { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

    /// <summary>A string granting exactly the purposes given.</summary>
    public static string ForPurposes(IEnumerable<int> consented)
    {
        var bytes = new byte[Bytes];
        foreach (var purpose in consented)
        {
            if (purpose < 1 || purpose > PurposeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(consented));
            }
            var bit = ConsentStart + purpose - 1;
            bytes[bit / 8] |= (byte)(1 << (7 - (bit % 8)));
        }
        // Nothing is granted on legitimate interest, so the run starting
        // here stays clear. It is named so the layout is readable rather
        // than being a number nobody can check.
        _ = LegitimateInterestStart;
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>A string granting everything, which decodes to
    /// personalized.</summary>
    public static string Personalized() => ForPurposes(AllPurposes);

    /// <summary>A string granting the standard set, which decodes to
    /// standard.</summary>
    public static string Standard() => ForPurposes(StandardPurposes);
}
