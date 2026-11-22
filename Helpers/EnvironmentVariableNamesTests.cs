using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Helpers
{
    /// <summary>
    /// Every environment variable the suite reads, or sets for an example it
    /// starts, is a _51DEGREES_ name, or a name that a browser, a driver or
    /// .NET defines for itself.
    /// </summary>
    [TestClass]
    public class EnvironmentVariableNamesTests
    {
        // Names other tools define, used as those tools require.
        private static readonly HashSet<string> ToolNames = new HashSet<string>
        {
            "CHROMEWEBDRIVER", "GECKOWEBDRIVER", "EDGEWEBDRIVER",
            "CHROME_BIN", "FIREFOX_BIN", "EDGE_BIN",
            "ASPNETCORE_URLS", "DOTNET_ROLL_FORWARD",
        };

        // A name read with GetEnvironmentVariable("..."), an upper-case name
        // set for an example as ["..."] = ..., and a constant whose
        // identifier ends in Variable or Var, which is how the suite names
        // its variables.
        private static readonly Regex[] Patterns =
        {
            new Regex(@"GetEnvironmentVariable\(\s*""([^""]+)"""),
            new Regex(@"\[\s*""([A-Z_][A-Z0-9_]*)""\s*\]\s*="),
            new Regex(@"const\s+string\s+\w*(?:Variable|Var)\s*=\s*""([^""]+)"""),
        };

        private static string SourceRoot()
        {
            var folder = new DirectoryInfo(AppContext.BaseDirectory);
            while (folder != null
                && folder.GetFiles("SeleniumApiTests.csproj").Length == 0)
            {
                folder = folder.Parent;
            }
            Assert.IsNotNull(folder, "the suite's source folder was not found");
            return folder.FullName;
        }

        [TestMethod]
        public void EveryNameIsA51DegreesNameOrAToolsOwn()
        {
            var root = SourceRoot();
            var wrong = new List<string>();
            var found = 0;
            foreach (var file in Directory.EnumerateFiles(
                root, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file);
                if (relative.StartsWith("bin", StringComparison.Ordinal)
                    || relative.StartsWith("obj", StringComparison.Ordinal)
                    || Path.GetFileName(file) == "EnvironmentVariableNamesTests.cs")
                {
                    continue;
                }
                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    foreach (var match in Patterns.SelectMany(
                        pattern => pattern.Matches(lines[index])))
                    {
                        var name = match.Groups[1].Value;
                        found++;
                        if (!name.StartsWith("_51DEGREES_", StringComparison.Ordinal)
                            && !ToolNames.Contains(name))
                        {
                            wrong.Add($"{relative}:{index + 1} {name}");
                        }
                    }
                }
            }
            Assert.IsTrue(found > 0, "no environment variable names were found");
            Assert.AreEqual(0, wrong.Count,
                "names that are neither _51DEGREES_ names nor a tool's own: "
                + string.Join(", ", wrong));
        }
    }
}
