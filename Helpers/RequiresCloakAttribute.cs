using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers
{
    /// <summary>
    /// Skips a test, or every test in a class, when CLOAK_DEBUGGER_ADDRESS
    /// names no CloakBrowser to attach to, and gives the variable to set as
    /// the reason.
    /// </summary>
    /// <remarks>
    /// The test is skipped before its initialize method runs, so no example
    /// is started for a test that could not use it.
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class RequiresCloakAttribute : ConditionBaseAttribute
    {
        /// <summary>Runs the test only where the condition is met.</summary>
        public RequiresCloakAttribute()
            : base(ConditionMode.Include)
        {
            IgnoreMessage = BrowserDrivers.CloakNotConfiguredMessage;
        }

        /// <inheritdoc/>
        public override bool IsConditionMet => BrowserDrivers.IsCloakConfigured;

        /// <inheritdoc/>
        public override string GroupName => nameof(RequiresCloakAttribute);
    }
}
