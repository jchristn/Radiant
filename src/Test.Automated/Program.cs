namespace Test.Automated
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Console runner for the Radiant test suites.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Entry point. Pass <c>--results &lt;path&gt;</c> to export JSON results.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>0 when all tests pass, 1 when any fail.</returns>
        public static async Task<int> Main(string[] args)
        {
            string? resultsPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--results" && i + 1 < args.Length)
                {
                    resultsPath = args[i + 1];
                    break;
                }
            }

            return await ConsoleRunner.RunAsync(
                RadiantSuites.All,
                resultsPath: resultsPath).ConfigureAwait(false);
        }
    }
}
