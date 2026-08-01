namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using global::Xunit;
    using global::Xunit.Abstractions;

    /// <summary>
    /// Runs each Radiant descriptor as its own theory row for per-test visibility.
    /// </summary>
    public sealed class RadiantTheoryTests
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>
        /// Create the theory test fixture.
        /// </summary>
        /// <param name="output">The xUnit output helper.</param>
        public RadiantTheoryTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        /// <summary>
        /// The set of non-skipped descriptors.
        /// </summary>
        /// <returns>Theory data of test cases.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in RadiantSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip) data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Run a single descriptor.
        /// </summary>
        /// <param name="testCase">The descriptor to run.</param>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            _Output.WriteLine("Running: " + testCase.DisplayName);
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
