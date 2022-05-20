using System;
using System.Collections.Generic;
using System.Linq;

namespace BorsukSoftware.Testing.Examples.ComplexXmlResultComparison
{
    /* This program demonstrates using the BorsukSoftware.ObjectFlattener.* / BorsukSoftware.Testing.Comparison.* libraries
	 * to look for differences between objects within an Xml document representing pricing requests. The example schema has 
	 * some header information which we don't perform comparisons on (through choice).
	 * 
	 * The ordering of the reqests within the document is unimportant. The complexity / specialisation here is that the risk
	 * values are represented in Xml using multiple attributes for a single value, but for comparison purposes the entire 
	 * node-attribute set needs to treated as the unit of comparison rather than comparing each attribute independently. To 
	 * this end, there's a custom flattener component which performs this data normalisation. 
	 * 
	 * To extend the example for your own use-case, the requirement would be to update this node flattening
	 *
	 * The expected changes are:
	 * 
	 * - Additional 6M option
	 * - 1M option -> different values
	 * - 12M option -> duplicate entries -> incomparable
     */
    partial class Program
    {
        static void Main(string[] args)
        {
            // actual - has 1 more entry insert in the middle and a property has been adjusted
            var expectedName = typeof(Program).Assembly.GetManifestResourceNames().Single(n => n.EndsWith("expected.xml"));
            var actualName = typeof(Program).Assembly.GetManifestResourceNames().Single(n => n.EndsWith("actual.xml"));

            var expected = LoadStream(expectedName);
            var actual = LoadStream(actualName);

            var underlyingComparer = new BorsukSoftware.Testing.Comparison.ObjectComparer()
            {
                ObjectComparerMismatchedKeysBehaviour = BorsukSoftware.Testing.Comparison.ObjectComparerMismatchedKeysBehaviour.ReportAsDifference,
                ObjectComparerNoAvailablePluginBehaviour = BorsukSoftware.Testing.Comparison.ObjectComparerNoAvailablePluginBehaviour.Throw
            };
            underlyingComparer.ComparisonPlugins.Add(new BorsukSoftware.Testing.Comparison.Plugins.SimpleStringComparerPlugin());
            underlyingComparer.ComparisonPlugins.Add(new BorsukSoftware.Testing.Comparison.Plugins.DoubleComparerPlugin());
            underlyingComparer.ComparisonPlugins.Add(new BorsukSoftware.Testing.Comparison.Plugins.DecimalComparerPlugin());

            var objectFlattener = new BorsukSoftware.ObjectFlattener.ObjectFlattener()
            {
                NoAvailablePluginBehaviour = BorsukSoftware.ObjectFlattener.NoAvailablePluginBehaviour.Throw
            };

            // Ordering matters here. We want to represent each risk atom as the unit of comparison rather tha node and its attributes as a single value with (as opposed to multiple entries).
            //
            // Hence we need to intercept the flattening of the risk related types 
            objectFlattener.Plugins.Add(new RiskNodeFlattener());

            // For everything else, fall back to the regular plugin
            objectFlattener.Plugins.Add(new BorsukSoftware.ObjectFlattener.Plugins.SystemXml.SystemXmlPlugin()
            {
                DuplicateElementNameBehaviour = ObjectFlattener.Plugins.SystemXml.DuplicateElementNameBehaviour.Throw
            });

            // Do the actual comparison
            var setComparer = new Comparison.Extensions.Collections.ObjectSetComparerStandard(objectFlattener, underlyingComparer);
            var results = setComparer.CompareObjectSets((idx, element) =>
            {
                // We define the keys for comparison as extracting the proeprty on the underlying Json object called 'key'
                var keys = new Dictionary<string, object>();

                var keyAttr = element.Attributes["key"];
                if (keyAttr != null)
                    keys["id"] = keyAttr.Value;

                return keys;
            },
                expected.SelectNodes("requests/request").Cast<System.Xml.XmlNode>(),
                actual.SelectNodes("requests/request").Cast<System.Xml.XmlNode>());

            // TODO - Temp output
            var tempOutputs = objectFlattener.FlattenObject(null, actual.SelectSingleNode("requests/request[@key='Vanilla-Put-EURGBP-1M-ATM']"));
            Console.WriteLine($"Temp outputs");
            foreach (var tempOutput in tempOutputs)
            {
                Console.WriteLine($" - {tempOutput.Key} = {tempOutput.Value}");
            }
            Console.WriteLine();
            Console.WriteLine();

            // Output the results
            Console.WriteLine($"Matching items - {results.MatchingObjects.Count}");
            Console.WriteLine($"Additional items - {results.AdditionalKeys.Count}");
            if (results.AdditionalKeys.Count > 0)
            {
                foreach (var key in results.AdditionalKeys)
                {
                    Console.WriteLine(" Item:");
                    foreach (var pair in key.Key)
                        Console.WriteLine($" - {pair.Key} = {pair.Value}");
                }
            }
            Console.WriteLine($"Missing items - {results.MissingKeys.Count}");
            if (results.MissingKeys.Count > 0)
            {
                foreach (var key in results.MissingKeys)
                {
                    Console.WriteLine(" Item:");
                    foreach (var pair in key.Key)
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
                    foreach (var pair in key.Value.Differences)
                    {
                        Console.WriteLine($" - {pair.Key}: {pair.Value.ExpectedValue} vs. {pair.Value.ActualValue}");
                    }
                }
            }

            bool passed =
                results.Differences.Count == 0 &&
                results.IncomparableKeys.Count == 0 &&
                results.AdditionalKeys.Count == 0 &&
                results.MissingKeys.Count == 0;

            // Sample output as XML blob
            //
            // Here, we're working on the assumption that we do want to see the surrounding information when a difference 
            // is found, i.e. just seeing the difference isn't enough, but we want to see the full request that we expected and
            // the one we actually received. 
            //
            // The choice of output format is entirely up to the end user. 
            //
            // Note that if a user is using Conical to display their results, then the expectation would be that they could have
            // full information in the XML document and then apply an XSLT at display time to allow them to create multiple views 
            // (or even interactive ones with JS) at display time.
            using (var fileStream = new System.IO.FileStream(@"C:\Temp\output.xml", FileMode.Create, FileAccess.Write))
            {
                using (var xmlWriter = System.Xml.XmlWriter.Create(fileStream, new System.Xml.XmlWriterSettings { Indent = true, IndentChars = "\t" }))
                {
                    xmlWriter.WriteStartDocument();
                    xmlWriter.WriteStartElement("comparison");
                    xmlWriter.WriteAttributeString("passed", passed.ToString());

                    xmlWriter.WriteStartElement("summary");
                    xmlWriter.WriteAttributeString("matching", results.MatchingObjects.Count.ToString());
                    xmlWriter.WriteAttributeString("differences", results.Differences.Count.ToString());
                    xmlWriter.WriteAttributeString("additionalKeys", results.AdditionalKeys.Count.ToString());
                    xmlWriter.WriteAttributeString("missingKeys", results.MissingKeys.Count.ToString());
                    xmlWriter.WriteAttributeString("incomparable", results.IncomparableKeys.Count.ToString());
                    xmlWriter.WriteEndElement();

                    OutputKeyCollection(xmlWriter, results.AdditionalKeys, "additional");
                    OutputKeyCollection(xmlWriter, results.MissingKeys, "missing");

                    if( results.IncomparableKeys.Any())
                    {
                        xmlWriter.WriteStartElement("incomparable");
                        foreach (var key in results.IncomparableKeys)
                        {
                            xmlWriter.WriteStartElement("entry");
                            xmlWriter.WriteAttributeString("id", key.Key["id"].ToString());

                            if( key.Value.ExpectedObjects.Any())
                            {
                                xmlWriter.WriteStartElement("expectedObjects");
                                foreach( var expectedObject in key.Value.ExpectedObjects)
                                {
                                    expectedObject.WriteTo(xmlWriter);
                                }
                                xmlWriter.WriteEndElement();
                            }

                            if (key.Value.ActualObjects.Any())
                            {
                                xmlWriter.WriteStartElement("actualObjects");
                                foreach (var expectedObject in key.Value.ActualObjects)
                                {
                                    expectedObject.WriteTo(xmlWriter);
                                }
                                xmlWriter.WriteEndElement();
                            }
                            xmlWriter.WriteEndElement();
                        }
                        xmlWriter.WriteEndElement();
                    }

                    if (results.Differences.Any())
                    {
                        xmlWriter.WriteStartElement("differences");
                        foreach (var differencePair in results.Differences)
                        {
                            xmlWriter.WriteStartElement("difference");
                            xmlWriter.WriteAttributeString("id", differencePair.Key["id"].ToString());

                            xmlWriter.WriteStartElement("differences");
                            foreach (var dif in differencePair.Value.Differences)
                            {
                                xmlWriter.WriteStartElement("difference");
                                xmlWriter.WriteAttributeString("key", dif.Key);
                                if (dif.Value.ExpectedValue != null)
                                    xmlWriter.WriteAttributeString("expected", dif.Value.ExpectedValue.ToString());
                                if (dif.Value.ActualValue != null)
                                    xmlWriter.WriteAttributeString("actual", dif.Value.ActualValue.ToString());
                                if (dif.Value.ComparisonPayload != null)
                                    xmlWriter.WriteAttributeString("dif", dif.Value.ComparisonPayload.ToString());

                                xmlWriter.WriteEndElement();
                            }
                            xmlWriter.WriteEndElement();


                            // Sometimes you'll also want the surrounding information, so we'll include it here for 
                            // demonstration purposes
                            xmlWriter.WriteStartElement("expected");
                            differencePair.Value.Expected.WriteTo(xmlWriter);
                            xmlWriter.WriteEndElement();
                            xmlWriter.WriteStartElement("actual");
                            differencePair.Value.Actual.WriteTo(xmlWriter);
                            xmlWriter.WriteEndElement();

                            xmlWriter.WriteEndElement();
                        }
                        xmlWriter.WriteEndElement();
                    }

                    xmlWriter.WriteEndElement();
                }
            }


        }
        static void OutputKeyCollection(System.Xml.XmlWriter xmlWriter, IReadOnlyDictionary<IReadOnlyDictionary<string, object>, System.Xml.XmlNode> nodes, string nodeTitle)
        {
            if (nodes.Any())
            {
                xmlWriter.WriteStartElement(nodeTitle);
                foreach (var pair in nodes)
                {
                    xmlWriter.WriteStartElement("item");
                    xmlWriter.WriteAttributeString("id", pair.Key["id"].ToString());
                    pair.Value.WriteTo(xmlWriter);
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();
            }
        }

        static System.Xml.XmlNode LoadStream(string name)
        {
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Unable to load stream - {name}");
                var xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.Load(stream);

                if (xmlDoc.DocumentElement == null)
                    throw new InvalidOperationException("No document element found on doc");

                return xmlDoc.DocumentElement;
            }
        }
    }
}
