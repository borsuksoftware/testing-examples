namespace BorsukSoftware.Testing.Examples.ComplexXmlResultComparison
{
    /// <summary>
    /// Custom flattener plugin to expands the 'risks' node into constituent parts using the following rules:
    /// 
    /// * Ordering is not important, explicitly, multiple fxvega 
    /// * Different risk types have different attributes for comparison
    /// </summary>
    public class RiskNodeFlattener : ObjectFlattener.IObjectFlatteningPlugin
    {
        public bool CanHandle(string prefix, object @object)
        {
            if (!(@object is System.Xml.XmlNode node))
                return false;

            return node.Name == "risks";
        }

        public IEnumerable<KeyValuePair<string, object>> FlattenObject(ObjectFlattener.IObjectFlattener objectFlattener, string prefix, object @object)
        {
            var riskNode = (System.Xml.XmlNode)@object;
            foreach (System.Xml.XmlNode riskSpecificNode in riskNode.ChildNodes)
            {
                var adjustedPrefix = string.IsNullOrEmpty(prefix) ? null : $"{prefix}.";

                var valueAttr = riskSpecificNode?.Attributes?["value"];
                object value = valueAttr?.Value ?? "uknown";
                if (!string.IsNullOrEmpty(valueAttr.Value) && decimal.TryParse(valueAttr.Value, out var valueD))
                    value = valueD;

                switch (riskSpecificNode.Name.ToLower())
                {
                    case "value":
                        {
                            var ccyAttr = riskSpecificNode?.Attributes?["ccy"];
                            var combinedName = $"{adjustedPrefix}value-{ccyAttr?.Value ?? "missing"}";
                            yield return new KeyValuePair<string, object>(combinedName, value);
                            break;
                        }

                    case "fxdelta":
                        {
                            var ccyAttr = riskSpecificNode?.Attributes?["ccy"];
                            var combinedName = $"{adjustedPrefix}fxdelta-{ccyAttr?.Value ?? "missing"}";
                            yield return new KeyValuePair<string, object>(combinedName, value);
                            break;
                        }

                    case "fxvega":
                        {
                            var ccyAttr = riskSpecificNode?.Attributes?["ccy"];
                            var ccyPairAttr = riskSpecificNode?.Attributes?["ccyPair"];
                            var expiryAttr = riskSpecificNode?.Attributes?["expiry"];
                            var combinedName = $"{adjustedPrefix}fxvega-{ccyPairAttr?.Value ?? "missing"}-{expiryAttr?.Value ?? "missing"}-{ccyAttr?.Value ?? "missing"}";
                            yield return new KeyValuePair<string, object>(combinedName, value);
                            break;
                        }

                    default:
                        throw new InvalidOperationException($"Unable to flatten risk node '{riskSpecificNode.Name}'");
                }
            }
        }
    }
}
