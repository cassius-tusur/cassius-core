using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BaseClasses.Enum;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services;
using BaseClasses.Services.Binds;
using Xunit;

namespace BaseClasses.Tests.Services
{
    /// <summary>
    /// Тесты для класса Serializer.
    /// </summary>
    public class SerializerTests
    {
        [Fact]
        public void JsonRoundTrip_ConvertsCompatibleParameterValues()
        {
            var element = new Element(ElemType.Character);
            var number = new ParamKey<double>("Number");
            var numbers = new ParamKey<List<double>>("Numbers");
            element.Params.Add((BaseClasses.Interface.IParamKey)number, 1);
            element.Params.Add((BaseClasses.Interface.IParamKey)numbers, new List<int> { 1, 2 });
            var restored = Serializer.DeserializeString<Element>(Serializer.SerializeToString(element));
            Assert.True(element.Params.TryGetValue(number, out var original));
            Assert.IsType<int>(original);
            Assert.True(restored.Params.TryGet(number, out var actual));
            Assert.Equal(1d, actual);
            Assert.True(restored.Params.TryGet(numbers, out var list));
            Assert.Equal(new[] { 1d, 2d }, list);
        }

        [Fact]
        public void JsonRoundTrip_ConvertsCompatibleRelationValue()
        {
            var plot = new Plot();
            var a = new Element(ElemType.Character);
            var b = new Element(ElemType.Character);
            plot.Add(a); plot.Add(b);
            plot.Bind(a, b, BaseRelationKeys.Relationship, 1);
            var restored = Serializer.DeserializeString<Plot>(Serializer.SerializeToString(plot));
            Assert.All(plot.Relations, relation => Assert.IsType<int>(relation.Value));
            Assert.Equal(2, restored.Relations.Count);
            Assert.All(restored.Relations, relation => Assert.Equal(1d, Assert.IsType<double>(relation.Value)));
        }
        [Theory]
        [InlineData("$type")]
        [InlineData("$value")]
        public void JsonSerialization_RejectsReservedNamesInParameterValues(string reservedName)
        {
            var key = new ParamKey<Dictionary<string, object>>("Data");
            var element = new Element(ElemType.Character);
            element.Params.Add(key, new Dictionary<string, object> { [reservedName] = 1 });
            var ex = Assert.Throws<JsonException>(() => Serializer.SerializeToString(element));
            Assert.Contains(reservedName, ex.Message);
        }

        [Fact]
        public void JsonSerialization_RejectsReservedNamesInsideNestedRelationValues()
        {
            var a = new Element(ElemType.Character);
            var b = new Element(ElemType.Character);
            var key = new ParamKey<object>("Custom");
            var value = new List<object> { new Dictionary<string, object> { ["$type"] = "int", ["$value"] = 123 } };
            var relation = new Relation(a, b, key, value);
            var ex = Assert.Throws<JsonException>(() => Serializer.SerializeToString(relation));
            Assert.Contains("$type", ex.Message);
        }

        [Fact]
        public void JsonSerialization_RejectsReservedPropertyNamesInObjects()
        {
            var element = new Element(ElemType.Character);
            element.Params.Add(new ParamKey<ReservedProperty>("Data"), new ReservedProperty());
            var ex = Assert.Throws<JsonException>(() => Serializer.SerializeToString(element));
            Assert.Contains("$value", ex.Message);
        }

        public sealed class ReservedProperty
        {
            [System.Text.Json.Serialization.JsonPropertyName("$value")]
            public int Value { get; set; }
        }
        [Fact]
        public void JsonRelations_RoundTripThroughMessagePackWithObjectValues()
        {
            var plot = new Plot();
            var a = new Element(ElemType.Character);
            var b = new Element(ElemType.Character);
            plot.Add(a); plot.Add(b);
            var key = new ParamKey<object>("nested");
            var id = Guid.NewGuid();
            plot.Bind(a, b, key, new Dictionary<string, object> { ["id"] = id, ["values"] = new List<object> { 123, id } });
            var jsonPlot = Serializer.DeserializeString<Plot>(Serializer.SerializeToString(plot));
            var mp = new PlotMessagePackSerializer();
            var restored = mp.Deserialize(mp.Serialize(jsonPlot));
            var data = Assert.IsType<Dictionary<string, object>>(Assert.Single(restored.Relations).Value);
            Assert.Equal(id, Assert.IsType<Guid>(data["id"]));
            Assert.Equal(123, Assert.IsType<int>(Assert.IsType<List<object>>(data["values"])[0]));
        }

        [Fact]
        public void PlainJsonRelationWithObjectValue_CanBeStoredInMessagePack()
        {
            var plot = new Plot();
            var a = new Element(ElemType.Character);
            var b = new Element(ElemType.Character);
            plot.Add(a); plot.Add(b);
            var json = $"{{\"Source\":\"{a.Id}\",\"Target\":\"{b.Id}\",\"Namespace\":\"Base\",\"Name\":\"value\",\"Type\":\"System.Object\",\"Value\":{{\"items\":[123,true]}}}}";
            plot.Relations.Add(Serializer.DeserializeString<Relation>(json, plot));
            var mp = new PlotMessagePackSerializer();
            var restored = mp.Deserialize(mp.Serialize(plot));
            var data = Assert.IsType<Dictionary<string, object>>(Assert.Single(restored.Relations).Value);
            Assert.Equal(123, Assert.IsType<List<object>>(data["items"])[0]);
        }

        [Fact]
        public void JsonAndMessagePack_SupportGenericObjectValues()
        {
            var key = new ParamKey<Dictionary<string, object>>("values");
            var element = new Element(ElemType.Character);
            element.Params.Add(key, new Dictionary<string, object> { ["set"] = new HashSet<int> { 1, 2 } });
            var fromJson = Serializer.DeserializeString<Element>(Serializer.SerializeToString(element));
            var serializer = new ElementMessagePackSerializer();
            var result = serializer.Deserialize(serializer.Serialize(fromJson));
            Assert.True(result.Params.TryGet(key, out var values));
            Assert.True(Assert.IsType<HashSet<int>>(values["set"]).SetEquals(new[] { 1, 2 }));
        }

        [Fact]
        public void LoadedUnsupportedRoute_CanBeUnboundOrRemoved()
        {
            var plot = new Plot();
            var a = new Element(ElemType.Character);
            var b = new Element(ElemType.Character);
            plot.Add(a); plot.Add(b);
            plot.Relations.Add(new Relation(a, b, BaseRelationKeys.Owns, true));
            var json = Serializer.SerializeToString(plot);
            var loaded = Serializer.DeserializeString<Plot>(json);
            var relation = Assert.Single(loaded.Relations);
            loaded.Unbind(relation.Source, relation.Target, relation.Param);
            Assert.Empty(loaded.Relations);
            Assert.Throws<ArgumentException>(() => loaded.Bind(relation.Source, relation.Target, relation.Param, true));
            loaded = Serializer.DeserializeString<Plot>(json);
            loaded.Remove(Assert.Single(loaded.Relations).Source);
            Assert.Empty(loaded.Relations);
        }
        [Fact]
        public void JsonRoundTrip_PreservesRuntimeTypesInsideObjectValues()
        {
            var key = new ParamKey<Dictionary<string, object>>("Values");
            var id = Guid.NewGuid();
            const decimal number = 0.1234567890123456789012345678m;
            var source = new Element(ElemType.Character);
            source.Params.Add(key, new Dictionary<string, object> {
                ["Long"] = 1L, ["Decimal"] = number, ["Id"] = id,
                ["Nested"] = new List<object> { 2L, number, id }
            });
            var json = Serializer.SerializeToString(source);
            Assert.DoesNotContain("ValueEncoding", json);
            var result = Serializer.DeserializeString<Element>(json);
            Assert.True(result.Params.TryGet(key, out var values));
            Assert.Equal(1L, Assert.IsType<long>(values["Long"]));
            Assert.Equal(number, Assert.IsType<decimal>(values["Decimal"]));
            Assert.Equal(id, Assert.IsType<Guid>(values["Id"]));
            var nested = Assert.IsType<List<object>>(values["Nested"]);
            Assert.Equal(number, Assert.IsType<decimal>(nested[1]));
            var messagePack = new ElementMessagePackSerializer();
            Assert.NotEmpty(messagePack.Serialize(result));
        }

        [Fact]
        public void LegacyJson_ObjectValuesRemainUsableByMessagePack()
        {
            const string json = "{\"Id\":\"22222222-2222-2222-2222-222222222222\",\"Type\":\"Character\",\"Name\":\"\",\"Description\":\"\",\"Time\":0,\"Params\":[{\"Namespace\":\"Base\",\"Name\":\"Values\",\"Type\":\"Dictionary<string, System.Object>\",\"Value\":{\"Number\":2,\"Nested\":{\"Text\":\"hello\"}}}]}";
            var result = Serializer.DeserializeString<Element>(json);
            Assert.True(result.Params.TryGet(new ParamKey<Dictionary<string, object>>("Values"), out var values));
            Assert.Equal(2, Assert.IsType<int>(values["Number"]));
            Assert.Equal("hello", Assert.IsType<Dictionary<string, object>>(values["Nested"])["Text"]);
            Assert.NotEmpty(new ElementMessagePackSerializer().Serialize(result));
        }
        private static string NewTempFilePath()
        {
            return Path.Combine(Path.GetTempPath(), $"baseclasses_{Guid.NewGuid():N}.json");
        }

        private static readonly ParamKey<string> _TraitsKey = new ParamKey<string>("Traits");
        private static readonly ParamKey<int> _WeightKey = new ParamKey<int>("Weight");

        [Fact]
        public void JsonRoundTrip_RestoresNestedObjectValues()
        {
            var key = new ParamKey<Dictionary<string, object>>("Data");
            var source = new Element(ElemType.Character);
            source.Params.Add(key, new Dictionary<string, object> {
                ["Nested"] = new Dictionary<string, object> { ["Values"] = new object[] { 2, false, "text" } }
            });
            var restored = Serializer.DeserializeString<Element>(Serializer.SerializeToString(source));
            Assert.True(restored.Params.TryGet(key, out var data));
            var nested = Assert.IsType<Dictionary<string, object>>(data["Nested"]);
            var values = Assert.IsType<object[]>(nested["Values"]);
            Assert.Equal(2, values[0]);
            Assert.Equal(false, values[1]);
            Assert.Equal("text", values[2]);
        }

        [Fact]
        public void JsonRoundTrip_RestoresObjectValuesUsableByMessagePack()
        {
            var key = new ParamKey<List<Dictionary<string, object>>>("Traits", "Procedural");
            var source = new Element(ElemType.Character);
            source.Params.Add(key, new List<Dictionary<string, object>> {
                new Dictionary<string, object> { ["Title"] = "Brave", ["Affection"] = 0.5, ["Count"] = 3, ["Active"] = true }
            });
            var fromJson = Serializer.DeserializeString<Element>(Serializer.SerializeToString(source));
            Assert.True(fromJson.Params.TryGet(key, out var traits));
            Assert.IsType<string>(traits[0]["Title"]);
            Assert.IsType<double>(traits[0]["Affection"]);
            Assert.IsType<int>(traits[0]["Count"]);
            Assert.IsType<bool>(traits[0]["Active"]);
            var messagePack = new ElementMessagePackSerializer();
            var restored = messagePack.Deserialize(messagePack.Serialize(fromJson));
            Assert.True(restored.Params.TryGet(key, out var result));
            Assert.Equal("Brave", result[0]["Title"]);
            Assert.Equal(0.5, result[0]["Affection"]);
        }

        [Fact]
        public void SerializeToString_Element_ContainsCoreFields()
        {
            var element = new Element(ElemType.Character, "Hero", "Main", time: 5);
            element.Params.Set(_TraitsKey, "Brave");

            var json = Serializer.SerializeToString(element);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("Id", out var idProp));
            Assert.Equal(JsonValueKind.String, idProp.ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(idProp.GetString()));

            Assert.Equal(nameof(ElemType.Character), root.GetProperty("Type").ToString());
            Assert.Equal("Hero", root.GetProperty("Name").GetString());
            Assert.Equal("Main", root.GetProperty("Description").GetString());
            Assert.Equal(5, root.GetProperty("Time").GetInt32());

            var paramsProp = root.GetProperty("Params");
            Assert.Equal(JsonValueKind.Array, paramsProp.ValueKind);
            Assert.Single(paramsProp.EnumerateArray());
            var item = paramsProp.EnumerateArray().First();
            Assert.Equal("Traits", item.GetProperty("Name").GetString());
            Assert.Equal("Brave", item.GetProperty("Value").GetString());
        }

        [Fact]
        public void SerializeToString_Plot_ContainsElementsAndRelationsDtoShape()
        {
            var plot = new Plot();
            var character = new Element(ElemType.Character, "Hero", "Main");
            var item = new Element(ElemType.Item, "Sword", "Steel");
            plot.Add(character);
            plot.Add(item);
            plot.Bind(character, item, BaseRelationKeys.Owns, true);
            plot.Time = 99;

            var json = Serializer.SerializeToString(plot);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(99, root.GetProperty("Time").GetInt32());

            var elements = root.GetProperty("Elements").EnumerateArray().ToList();
            Assert.Equal(2, elements.Count);
            Assert.All(elements, e =>
            {
                Assert.True(e.TryGetProperty("Id", out var id));
                Assert.Equal(JsonValueKind.String, id.ValueKind);
                Assert.True(e.TryGetProperty("Type", out _));
                Assert.True(e.TryGetProperty("Name", out _));
                Assert.True(e.TryGetProperty("Description", out _));
                Assert.True(e.TryGetProperty("Params", out _));
                Assert.True(e.TryGetProperty("Time", out _));
            });

            var relations = root.GetProperty("Relations").EnumerateArray().ToList();
            Assert.Equal(2, relations.Count);
            Assert.All(relations, r =>
            {
                Assert.Equal(JsonValueKind.String, r.GetProperty("Source").ValueKind);
                Assert.Equal(JsonValueKind.String, r.GetProperty("Target").ValueKind);
                Assert.Equal("Base", r.GetProperty("Namespace").GetString());
                Assert.Equal(JsonValueKind.String, r.GetProperty("Name").ValueKind);
                Assert.Equal(JsonValueKind.String, r.GetProperty("Type").ValueKind);
                Assert.Equal(JsonValueKind.True, r.GetProperty("Value").ValueKind);
            });
            Assert.Contains(relations, r => r.GetProperty("Name").GetString() == "Owns");
            Assert.Contains(relations, r => r.GetProperty("Name").GetString() == "Owned");
        }

        [Fact]
        public void SerializeAndDeserialize_Element_RoundTripPreservesData()
        {
            var path = NewTempFilePath();
            try
            {
                var source = new Element(ElemType.Item, "Sword", "Steel", time: 11);
                source.Params.Set(_WeightKey, 3);

                Serializer.Serialize(source, path);
                var actual = Serializer.Deserialize<Element>(path);

                Assert.Equal(source.Type, actual.Type);
                Assert.Equal(source.Name, actual.Name);
                Assert.Equal(source.Description, actual.Description);
                Assert.Equal(source.Time, actual.Time);
                Assert.True(actual.Params.TryGet(_WeightKey, out var weight));
                Assert.Equal(3, weight);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void SerializeAndDeserialize_Element_RoundTripPreservesPublicId()
        {
            var path = NewTempFilePath();
            try
            {
                var source = new Element(ElemType.Location, "Castle");

                Serializer.Serialize(source, path);
                var restored = Serializer.Deserialize<Element>(path);

                Assert.Equal(source.Id, restored.Id);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void SerializeAndDeserialize_Plot_RoundTripPreservesElementsRelationsAndTime()
        {
            var path = NewTempFilePath();
            try
            {
                var plot = new Plot();
                var character = new Element(ElemType.Character, "Hero", "Main");
                var item = new Element(ElemType.Item, "Sword", "Steel");
                var location = new Element(ElemType.Location, "Castle", "Stone");

                plot.Add(character);
                plot.Add(item);
                plot.Add(location);

                plot.Bind(character, item, BaseRelationKeys.Owns, true);
                plot.Bind(item, location, BaseRelationKeys.Located, true);
                plot.Time = 42;

                Serializer.Serialize(plot, path);
                var actual = Serializer.Deserialize<Plot>(path);

                Assert.Equal(3, actual.Elements.Count);
                Assert.Equal(42, actual.Time);
                Assert.Equal(4, actual.Relations.Count);

                Assert.Contains(actual.Relations, r =>
                    r.Source.Type == ElemType.Character &&
                    r.Target.Type == ElemType.Item &&
                    r.Param.Equals(BaseRelationKeys.Owns) &&
                    r.Value is true);

                Assert.Contains(actual.Relations, r =>
                    r.Source.Type == ElemType.Item &&
                    r.Target.Type == ElemType.Character &&
                    r.Param.Equals(BaseRelationKeys.Owned) &&
                    r.Value is true);

                Assert.Contains(actual.Relations, r =>
                    r.Source.Type == ElemType.Item &&
                    r.Target.Type == ElemType.Location &&
                    r.Param.Equals(BaseRelationKeys.Located));

                Assert.Contains(actual.Relations, r =>
                    r.Source.Type == ElemType.Location &&
                    r.Target.Type == ElemType.Item &&
                    r.Param.Equals(BaseRelationKeys.Locates));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Deserialize_Plot_WithMissingRelationSource_ThrowsJsonException()
        {
            var path = NewTempFilePath();
            try
            {
                // Source/Target не существуют в коллекции Elements.
                var json = @"{
  ""Time"": 0,
  ""Elements"": [],
  ""Relations"": [
    {
      ""Source"": ""11111111-1111-1111-1111-111111111111"",
      ""Target"": ""22222222-2222-2222-2222-222222222222"",
      ""Namespace"": ""Base"",
      ""Name"": ""Owns"",
      ""Type"": ""System.Boolean"",
      ""Value"": true
    }
  ]
}";
                File.WriteAllText(path, json);

                Assert.Throws<JsonException>(() => Serializer.Deserialize<Plot>(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Deserialize_UnsupportedType_ThrowsArgumentException()
        {
            var path = NewTempFilePath();
            try
            {
                File.WriteAllText(path, "{}");

                Assert.Throws<ArgumentException>(() => Serializer.Deserialize<Dictionary<string, string>>(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void PrintToString_Element_ReturnsReadableText()
        {
            var element = new Element(ElemType.Event, "Battle", "Finale");

            var actual = Serializer.PrintToString(element);

            Assert.Contains("Event: Battle", actual);
            Assert.Contains("Finale", actual);
        }

        [Fact]
        public void PrintToString_Plot_ContainsTimeElementsAndRelationsSections()
        {
            var plot = new Plot();
            var character = new Element(ElemType.Character, "Hero", "Main");
            var item = new Element(ElemType.Item, "Sword", "Steel");
            plot.Add(character);
            plot.Add(item);
            plot.Bind(character, item, BaseRelationKeys.Owns, true);

            var actual = Serializer.PrintToString(plot);

            Assert.Contains("Time:", actual);
            Assert.Contains("Elements:", actual);
            Assert.Contains("Relations:", actual);
            Assert.Contains("Character: Hero", actual);
            Assert.Contains("Item: Sword", actual);
        }
    }
}
