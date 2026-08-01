namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs each Radiant descriptor via a test-case source for per-test visibility.
    /// </summary>
    [TestFixture]
    public sealed class RadiantNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(RadiantSuites.All);
        }

        /// <summary>
        /// Run a single descriptor.
        /// </summary>
        /// <param name="testCase">The descriptor to run.</param>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
