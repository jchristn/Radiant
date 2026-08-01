namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// Runs every Radiant descriptor sequentially through the Touchstone executor in a single fact.
    /// </summary>
    public sealed class RadiantFactTests : TouchstoneFactBase
    {
        /// <summary>
        /// The suites under test.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return RadiantSuites.All; }
        }

        /// <summary>
        /// Run all suites.
        /// </summary>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
