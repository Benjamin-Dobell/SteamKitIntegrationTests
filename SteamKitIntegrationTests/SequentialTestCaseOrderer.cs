using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace SteamKitIntegrationTests
{
	public class OrderedFactAttribute : FactAttribute
	{
		public int Order { get; }

		public OrderedFactAttribute([CallerLineNumber] int lineNumber = 0)
		{
			Order = lineNumber;
		}
	}

	public class SequentialTestCaseOrderer : ITestCaseOrderer
	{
		public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
		{
			return testCases.OrderBy(testCase => {
				var method = ((ReflectionMethodInfo) testCase.TestMethod.Method).MethodInfo;
				OrderedFactAttribute orderedFactAttribute = (OrderedFactAttribute) method.GetCustomAttributes(typeof(OrderedFactAttribute)).First();
				return orderedFactAttribute.Order;
			});
		}
	}
}
