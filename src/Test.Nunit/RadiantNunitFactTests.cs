namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs every Radiant descriptor sequentially through the Touchstone executor in a single test.
    /// </summary>
    [TestFixture]
    public sealed class RadiantNunitFactTests : TouchstoneNunitBase
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
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
