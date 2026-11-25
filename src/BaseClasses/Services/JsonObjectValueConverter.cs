using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BaseClasses.Services
{
    // JSON has no CLR type metadata for object values; infer types from the JSON tokens.
    internal sealed class JsonObjectValueConverter : JsonConverter<object>
    {
        internal static void ValidatePropertyNames(object? value, Type type, JsonSerializerOptions options)
        {
            var json = JsonSerializer.SerializeToElement(value, type, options);
            ValidatePropertyNames(json);
        }

        private static void ValidatePropertyNames(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in value.EnumerateObject())
                {
                    if (property.Name == "$type" || property.Name == "$value")
                        throw new JsonException($"JSON property name '{property.Name}' is reserved for type metadata.");
                    ValidatePropertyNames(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray()) ValidatePropertyNames(item);
            }
        }

        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return ReadValue(document.RootElement, options);
        }

        private static object? ReadValue(JsonElement value, JsonSerializerOptions options)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Null: return null;
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.String: return value.GetString();
                case JsonValueKind.Number:
                    var text = value.GetRawText();
                    if (text.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
                    {
                        if (value.TryGetInt32(out var intValue)) return intValue;
                        if (value.TryGetInt64(out var longValue)) return longValue;
                    }
                    return value.GetDouble();
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in value.EnumerateArray()) list.Add(ReadValue(item, options));
                    return list;
                case JsonValueKind.Object:
                    if (value.TryGetProperty("$type", out var typeProperty) &&
                        value.TryGetProperty("$value", out var valueProperty) &&
                        value.EnumerateObject().Count() == 2)
                    {
                        var name = typeProperty.GetString() ?? throw new JsonException("Object type cannot be null.");
                        var type = TypeNameHelper.ResolveType(name);
                        if (type == typeof(object)) return new object();
                        return JsonSerializer.Deserialize(valueProperty.GetRawText(), type, options);
                    }
                    var dictionary = new Dictionary<string, object?>();
                    foreach (var property in value.EnumerateObject()) dictionary.Add(property.Name, ReadValue(property.Value, options));
                    return dictionary;
                default: throw new JsonException("Unsupported object value.");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            var type = value.GetType();
            var name = TypeNameHelper.GetTypeName(type);
            if (TypeNameHelper.ResolveType(name) != type)
                throw new JsonException($"Object type '{type}' cannot be restored unambiguously.");
            writer.WriteStartObject();
            writer.WriteString("$type", name);
            writer.WritePropertyName("$value");
            if (type == typeof(object)) { writer.WriteStartObject(); writer.WriteEndObject(); }
            else JsonSerializer.Serialize(writer, value, type, options);
            writer.WriteEndObject();
        }
    }
}
