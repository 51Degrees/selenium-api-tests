using System;
using System.IO;

namespace FiftyOne.Pipeline.Cloud.SeleniumTests.Examples
{
    /// <summary>
    /// Locates sibling repositories at runtime. This tests repo is checked out
    /// alongside the example repos, so the examples live in directories that
    /// share this repo's parent.
    /// </summary>
    public static class RepoPaths
    {
        /// <summary>
        /// Directory that holds this repo and its sibling example repos.
        /// Override with the SELENIUM_SIBLINGS_ROOT environment variable.
        /// </summary>
        public static string SiblingsRoot { get; } = ResolveSiblingsRoot();

        private static string ResolveSiblingsRoot()
        {
            var overridden = Environment.GetEnvironmentVariable("SELENIUM_SIBLINGS_ROOT");
            if (!string.IsNullOrEmpty(overridden))
            {
                return overridden;
            }

            var repoRoot = FindUp("SeleniumApiTests.csproj");
            return Directory.GetParent(repoRoot)?.FullName
                ?? throw new InvalidOperationException(
                    $"Could not determine the siblings root above '{repoRoot}'.");
        }

        private static string FindUp(string marker)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, marker)))
            {
                dir = dir.Parent;
            }
            return dir?.FullName
                ?? throw new InvalidOperationException(
                    $"Could not locate the repo root: no '{marker}' found above " +
                    $"'{AppContext.BaseDirectory}'.");
        }
    }
}
