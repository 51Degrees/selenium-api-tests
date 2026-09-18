using System;
using FiftyOne.SeleniumTests.TestLogger;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using PlatformTestResult =
    Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers
{
    /// <summary>What the skip reason logger prints.</summary>
    [TestClass]
    public class SkipReasonLoggerTests
    {
        private static PlatformTestResult Result(
            TestOutcome outcome, string message)
        {
            var testCase = new TestCase(
                "Suite.Class.TheTest",
                new Uri("executor://mstest/v2"),
                "SeleniumApiTests.dll")
            {
                DisplayName = "TheTest",
            };
            return new PlatformTestResult(testCase)
            {
                Outcome = outcome,
                ErrorMessage = message,
            };
        }

        /// <summary>A skip prints its test and its reason on one line.</summary>
        [TestMethod]
        public void Format_Skipped_PrintsTheReason()
        {
            var line = SkipReasonLogger.Format(Result(
                TestOutcome.Skipped,
                "Assert.Inconclusive failed. No example descriptor "
                + "registered for EXAMPLE_LANG='cobol'."));

            Assert.AreEqual(
                "  Skipped TheTest, because: No example descriptor "
                + "registered for EXAMPLE_LANG='cobol'.",
                line);
        }

        /// <summary>A passing test prints nothing.</summary>
        [TestMethod]
        public void Format_Passed_PrintsNothing()
        {
            Assert.IsNull(SkipReasonLogger.Format(
                Result(TestOutcome.Passed, "some output")));
        }

        /// <summary>A skip with no reason prints nothing.</summary>
        [TestMethod]
        public void Format_SkippedWithNoReason_PrintsNothing()
        {
            Assert.IsNull(SkipReasonLogger.Format(
                Result(TestOutcome.Skipped, "  ")));
        }

        /// <summary>
        /// A reason raised in a class or test initialiser arrives wrapped by
        /// the test framework and spread over lines. It still reads as one
        /// line naming the cause.
        /// </summary>
        [TestMethod]
        public void Format_ReasonFromAnInitialiser_ReadsAsOneLine()
        {
            var line = SkipReasonLogger.Format(Result(
                TestOutcome.Skipped,
                "Class Initialization method Some.Class.ClassInit threw "
                + "exception.\r\nMicrosoft.VisualStudio.TestTools."
                + "UnitTesting.AssertInconclusiveException: "
                + "Assert.Inconclusive failed. Required environment variable "
                + "'CLOUD_ROOT_URL' is not set.."));

            StringAssert.Contains(line, "'CLOUD_ROOT_URL' is not set.");
            Assert.IsFalse(line.Contains("AssertInconclusiveException"),
                "the exception type is noise, not a reason");
            Assert.IsFalse(line.Contains('\n'), "one skip should be one line");
        }
    }
}
