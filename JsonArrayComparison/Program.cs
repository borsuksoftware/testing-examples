using System;
using System.Collections.Generic;
using System.Linq;

namespace BorsukSoftware.Testing.Examples.JsonArrayComparison
{
	/*
	 * This program demonstrates using the BorsukSoftware.ObjectFlattener.* / BorsukSoftware.Testing.Comparison.* libraries
	 * to look for differences between objects in a Json array (using Json.net) where the ordering isn't important and the 
	 * objects are identified by a named property within the container Json object
	 * 
	 * The example source material for comparison is in the resources directory with:
	 * 
	 *  - An additional object - key='Object 17'
	 *  - Changes to the value of hack.nestedProp for key='Object 7'
	 * 
	 * We use Json.net (as opposed to System.Text.Json) as the customer was already using Json.net in their testing. The
	 * System.Text.Json equivalent would be very similar.
	 */ 
	class Program
	{
		static void Main(string[] args)
		{
			// actual - has 1 more entry insert in the middle and a property has been adjusted
			var expectedName = typeof(Program).Assembly.GetManifestResourceNames().Single(n => n.EndsWith("expected.json"));
			var actualName = typeof(Program).Assembly.GetManifestResourceNames().Single(n => n.EndsWith("actual.json"));

			var expected = LoadStream(expectedName);
			var actual = LoadStream(actualName);

			var underlyingComparer = new BorsukSoftware.Testing.Comparison.ObjectComparer()
			{
				ObjectComparerMismatchedKeysBehaviour = BorsukSoftware.Testing.Comparison.ObjectComparerMismatchedKeysBehaviour.ReportAsDifference,
				ObjectComparerNoAvailablePluginBehaviour = BorsukSoftware.Testing.Comparison.ObjectComparerNoAvailablePluginBehaviour.Throw
			};
			underlyingComparer.ComparisonPlugins.Add(new BorsukSoftware.Testing.Comparison.Plugins.SimpleStringComparerPlugin());
			underlyingComparer.ComparisonPlugins.Add(new BorsukSoftware.Testing.Comparison.Plugins.DoubleComparerPlugin());

			var objectFlattener = new BorsukSoftware.ObjectFlattener.ObjectFlattener()
			{
				NoAvailablePluginBehaviour = BorsukSoftware.ObjectFlattener.NoAvailablePluginBehaviour.Throw
			};
			// Ordering matters here, so put Json extractor first
			objectFlattener.Plugins.Add(new BorsukSoftware.ObjectFlattener.Plugins.JsonDotNetPlugin());

			// If you were flattening .net objects, you'd use the following
			// objectFlattener.Plugins.Add(new BorsukSoftware.ObjectFlattener.Plugins.StandardPlugin());

			var setComparer = new BorsukSoftware.Testing.Comparison.Extensions.Collections.ObjectSetComparer(objectFlattener, underlyingComparer);

			// Note that expected / actual implement IEnumerable<JToken> hence we perform the comparison on a JToken (JObjects are a child class)
			var results = setComparer.CompareObjectSets<Newtonsoft.Json.Linq.JToken>((idx, jobject) =>
			{
				// We define the keys for comparison as extracting the proeprty on the underlying Json object called 'key'
				var keys = new Dictionary<string, object>();

				if (jobject is Newtonsoft.Json.Linq.JObject jo)
					keys["key"] = jo.Property("key").Value;
				return keys;
			},
				expected,
				actual);

			// Output the results
			Console.WriteLine($"Matching items - {results.MatchingKeysCount}");
			Console.WriteLine($"Additional items - {results.AdditionalKeys.Count}");
			if(results.AdditionalKeys.Count > 0)
			{
				foreach( var key in results.AdditionalKeys)
				{
					Console.WriteLine(" Item:");
					foreach (var pair in key)
						Console.WriteLine($" - {pair.Key} = {pair.Value}");
				}
			}
			Console.WriteLine($"Differences - {results.Differences.Count}");
			if (results.Differences.Count > 0)
			{
				foreach (var key in results.Differences)
				{
					Console.WriteLine(" Item:");
					foreach (var pair in key.Key)
						Console.WriteLine($" - {pair.Key} = {pair.Value}");

					Console.WriteLine(" Diffs:");
					foreach( var pair in key.Value)
					{
						Console.WriteLine($" - {pair.Key}: {pair.Value.ExpectedValue} vs. {pair.Value.ActualValue}");
					}
				}
			}
		}

		static Newtonsoft.Json.Linq.JArray LoadStream(string name)
		{
			using (var stream = typeof(Program).Assembly.GetManifestResourceStream(name))
			{
				var serializer = Newtonsoft.Json.JsonSerializer.Create();
				using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StreamReader(stream)))
					return serializer.Deserialize<Newtonsoft.Json.Linq.JArray>(reader);
			}
		}
	}
}
