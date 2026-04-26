using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;

using LiveSplit.Options;

namespace LiveSplit.Web;

public static class JSON
{
    public static dynamic FromStream(Stream stream)
    {
        var reader = new StreamReader(stream);
        string json = "";
        try
        {
            json = reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }

        return FromString(json);
    }

    public static dynamic FromString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        // Match JavaScriptSerializer's lenient parsing: tolerate trailing commas and JS
        // comments. Speedrun.com responses are well-formed today, but autosplitter
        // configs in the wild rely on these affordances and would silently fail otherwise.
        var options = new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        JsonNode node = JsonNode.Parse(value, documentOptions: options);
        return NodeToPoco(node);
    }

    // Convert a System.Text.Json node into the (Dictionary<string, object> / List<object> /
    // primitive / null) shape that the legacy JavaScriptSerializer produced, wrapping
    // object-nodes in DynamicJsonObject so existing `dynamic` consumers keep working.
    private static object NodeToPoco(JsonNode node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonObject obj)
        {
            var dict = new Dictionary<string, object>();
            foreach (KeyValuePair<string, JsonNode> kvp in obj)
            {
                dict[kvp.Key] = NodeToPoco(kvp.Value);
            }

            return new DynamicJsonObject(dict);
        }

        if (node is JsonArray arr)
        {
            var list = new List<object>(arr.Count);
            foreach (JsonNode item in arr)
            {
                list.Add(NodeToPoco(item));
            }

            return list;
        }

        if (node is JsonValue val)
        {
            JsonElement element = val.GetValue<JsonElement>();
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    // Match the legacy JavaScriptSerializer: integers map to long, all other
                    // numerics map to double. Promoting to decimal first changed the runtime
                    // type seen by downstream `dynamic` consumers and broke arithmetic on PB
                    // times / segment durations that were stored as fractional seconds.
                    if (element.TryGetInt64(out long l))
                    {
                        return l;
                    }

                    return element.GetDouble();
                case JsonValueKind.Null:
                    return null;
            }
        }

        return null;
    }

}

public sealed class DynamicJsonObject : DynamicObject
{
    private readonly IDictionary<string, object> _dictionary;

    //public IDictionary<string, object> Properties { get { return _dictionary; } }

    public DynamicJsonObject()
        : this(new Dictionary<string, object>())
    { }

    public DynamicJsonObject(IDictionary<string, object> dictionary)
    {
        if (dictionary == null)
        {
            throw new ArgumentNullException(nameof(dictionary));
        }

        _dictionary = dictionary;
    }

    public override string ToString()
    {
        var sb = new StringBuilder("{\r\n");
        ToString(sb);
        return sb.ToString();
    }

    private void ToString(StringBuilder sb, int depth = 1)
    {
        bool firstInDictionary = true;
        foreach (KeyValuePair<string, object> pair in _dictionary)
        {
            if (!firstInDictionary)
            {
                sb.Append(",\r\n");
            }

            sb.Append('\t', depth);
            firstInDictionary = false;
            object value = pair.Value;
            string name = pair.Key;
            if (value == null)
            {
                sb.AppendFormat("\"{0}\": {1}", HttpUtility.JavaScriptStringEncode(name), "null");
            }
            else if (value is IEnumerable<object> array)
            {
                sb.Append("\"" + HttpUtility.JavaScriptStringEncode(name) + "\": [\r\n");
                bool firstInArray = true;
                foreach (object arrayValue in array)
                {
                    if (!firstInArray)
                    {
                        sb.Append(",\r\n");
                    }

                    sb.Append('\t', depth + 1);
                    firstInArray = false;
                    if (arrayValue is IDictionary<string, object> dict)
                    {
                        new DynamicJsonObject(dict).ToString(sb, depth + 2);
                    }
                    else if (arrayValue is DynamicJsonObject obj)
                    {
                        sb.Append("{\r\n");
                        obj.ToString(sb, depth + 2);
                    }
                    else if (arrayValue is string str)
                    {
                        sb.AppendFormat("\"{0}\"", HttpUtility.JavaScriptStringEncode(str));
                    }
                    else if (arrayValue is decimal m)
                    {
                        sb.AppendFormat("{0}", HttpUtility.JavaScriptStringEncode(m.ToString(CultureInfo.InvariantCulture)));
                    }
                    else
                    {
                        sb.AppendFormat("\"{0}\"", HttpUtility.JavaScriptStringEncode((arrayValue ?? "").ToString()));
                    }
                }

                sb.Append("\r\n");
                sb.Append('\t', depth);
                sb.Append("]");
            }
            else if (value is IDictionary<string, object> dict)
            {
                sb.Append("\"" + HttpUtility.JavaScriptStringEncode(name) + "\": {\r\n");
                new DynamicJsonObject(dict).ToString(sb, depth + 1);
            }
            else if (value is DynamicJsonObject obj)
            {
                sb.Append("\"" + HttpUtility.JavaScriptStringEncode(name) + "\": {\r\n");
                obj.ToString(sb, depth + 1);
            }
            else if (value is string str)
            {
                sb.AppendFormat("\"{0}\": \"{1}\"", HttpUtility.JavaScriptStringEncode(name), HttpUtility.JavaScriptStringEncode(str));
            }
            else if (value is bool b)
            {
                sb.AppendFormat("\"{0}\": {1}", HttpUtility.JavaScriptStringEncode(name), b ? "true" : "false");
            }
            else if (IsLongType(value))
            {
                sb.AppendFormat("\"{0}\": {1}", HttpUtility.JavaScriptStringEncode(name), HttpUtility.JavaScriptStringEncode(Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture)));
            }
            else if (IsULongType(value))
            {
                sb.AppendFormat("\"{0}\": {1}", HttpUtility.JavaScriptStringEncode(name), HttpUtility.JavaScriptStringEncode(Convert.ToUInt64(value).ToString(CultureInfo.InvariantCulture)));
            }
            else if (IsDoubleType(value))
            {
                sb.AppendFormat("\"{0}\": {1}", HttpUtility.JavaScriptStringEncode(name), HttpUtility.JavaScriptStringEncode(Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture)));
            }
            else if (value is decimal m)
            {
                sb.AppendFormat("\"{0}\": {1}", HttpUtility.JavaScriptStringEncode(name), HttpUtility.JavaScriptStringEncode(m.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                sb.AppendFormat("\"{0}\": \"{1}\"", HttpUtility.JavaScriptStringEncode(name), HttpUtility.JavaScriptStringEncode((value ?? "").ToString()));
            }
        }

        sb.Append("\r\n");
        sb.Append('\t', depth - 1);
        sb.Append("}");
    }

    private static bool IsLongType(object value)
    {
        return value is sbyte or short or int or long;
    }

    private static bool IsULongType(object value)
    {
        return value is byte or ushort or uint or ulong;
    }

    private static bool IsDoubleType(object value)
    {
        return value is float or double;
    }

    public override bool TrySetMember(SetMemberBinder binder, object value)
    {
        if (_dictionary.ContainsKey(binder.Name))
        {
            _dictionary[binder.Name] = value;
            return true;
        }
        else
        {
            _dictionary.Add(binder.Name, value);
            return true;
        }
    }

    public override bool TryGetMember(GetMemberBinder binder, out object result)
    {
        if (binder.Name == "Properties")
        {
            result = _dictionary
                .Select(x => new KeyValuePair<string, dynamic>(x.Key, WrapResultObject(x.Value)))
                .ToDictionary(x => x.Key, x => x.Value);
            return true;
        }

        if (!_dictionary.TryGetValue(binder.Name, out result))
        {
            // return null to avoid exception.  caller can check for null this way...
            result = null;
            return true;
        }

        result = WrapResultObject(result);

        if (result is string)
        {
            result = JavaScriptStringDecode(result as string);
        }

        return true;
    }

    public static string JavaScriptStringDecode(string source)
    {
        // Replace some chars.
        string decoded = source.Replace(@"\'", "'")
                    .Replace(@"\""", @"""")
                    .Replace(@"\/", "/")
                    .Replace(@"\t", "\t")
                    .Replace(@"\n", "\n");

        // Replace unicode escaped text.
        var rx = new Regex(@"\\[uU]([0-9A-F]{4})");

        decoded = rx.Replace(decoded, match => ((char)int.Parse(match.Value[2..], NumberStyles.HexNumber))
                                                .ToString(CultureInfo.InvariantCulture));

        return decoded;
    }

    public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
    {
        if (indexes.Length == 1 && indexes[0] != null)
        {
            if (!_dictionary.TryGetValue(indexes[0].ToString(), out result))
            {
                // return null to avoid exception.  caller can check for null this way...
                result = null;
                return true;
            }

            result = WrapResultObject(result);
            return true;
        }

        return base.TryGetIndex(binder, indexes, out result);
    }

    private static object WrapResultObject(object result)
    {
        if (result is IDictionary<string, object> dictionary)
        {
            return new DynamicJsonObject(dictionary);
        }

        if (result is IList<object> list && list.Count > 0)
        {
            return list[0] is IDictionary<string, object>
                ? new List<object>(list.Cast<IDictionary<string, object>>().Select(x => new DynamicJsonObject(x)))
                : new List<object>(list);
        }

        return result;
    }
}
