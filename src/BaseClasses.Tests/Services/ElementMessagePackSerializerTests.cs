using System;
using System.Collections.Generic;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services;
using Xunit;

namespace BaseClasses.Tests.Services
{
    public class ElementMessagePackSerializerTests
    {
        [Fact]
        public void RoundTripPreservesElementSubclassIdentity()
        {
            var source = new DerivedElement(Guid.NewGuid(), ElemType.Location);
            var serializer = new ElementSerializer();
            serializer.Register(new DerivedElementSerializer());
            var result = Assert.IsType<DerivedElement>(serializer.Deserialize(serializer.Serialize(source)));
            Assert.Equal(source.Id, result.Id);
        }

        [Fact]
        public void RoundTripPreservesNullableAndConvertedParameters()
        {
            var source = new Element(ElemType.Location);
            var nullableKey = new ParamKey<int?>("nullable");
            var convertedKey = new ParamKey<double>("converted");
            var nullKey = new ParamKey<int>("null");
            source.Params.Add(nullableKey, (int?)42);
            source.Params.Add((IParamKey)convertedKey, 3);
            source.Params.Add((IParamKey)nullKey, null);
            var serializer = new ElementMessagePackSerializer();
            var restored = serializer.Deserialize(serializer.Serialize(source));
            Assert.True(restored.Params.TryGet(nullableKey, out int? nullable));
            Assert.Equal(42, nullable);
            Assert.True(restored.Params.TryGet(convertedKey, out double converted));
            Assert.Equal(3d, converted);
            Assert.True(restored.Params.TryGetValue(nullKey, out var nullValue));
            Assert.Null(nullValue);
        }

        [Fact]
        public void GenericSetParameterRoundTrips()
        {
            var element = new Element(ElemType.Location);
            var key = new ParamKey<HashSet<int>>("set");
            element.Params.Add(key, new HashSet<int> { 1 });
            var serializer = new ElementMessagePackSerializer();
            var restored = serializer.Deserialize(serializer.Serialize(element));
            Assert.True(restored.Params.TryGet(key, out var set));
            Assert.True(set.SetEquals(new[] { 1 }));
        }

        private sealed class DerivedElement : Element
        {
            public DerivedElement(Guid id, ElemType type) : base(id, type, "", "", -1) { }
        }

        private sealed class DerivedElementSerializer : ElementMessagePackSerializerBase
        {
            protected override string ImplementationId => "test.derived";
            protected override Type ElementType => typeof(DerivedElement);
            protected override byte[] SerializeExtension(IElement element) => Array.Empty<byte>();
            protected override IElement DeserializeExtension(ElementMessagePackData common, byte[] extension) =>
                new DerivedElement(common.Id, common.Type);
        }

        [Fact]
        public void DispatcherRoundTripsDifferentImplementations()
        {
            var serializer = new ElementSerializer();
            serializer.Register(new CustomSerializer());
            IElement ordinary = new Element(ElemType.Location, "Town");
            IElement custom = new CustomElement(Guid.NewGuid(), ElemType.Location, "Castle", 42);
            Assert.IsType<Element>(serializer.Deserialize(serializer.Serialize(ordinary)));
            var restored = Assert.IsType<CustomElement>(serializer.Deserialize(serializer.Serialize(custom)));
            Assert.Equal(custom.Id, restored.Id);
            Assert.Equal(42, restored.Danger);
        }

        [Fact]
        public void DispatcherRejectsUnknownImplementationsAndDuplicateRegistrations()
        {
            var serializer = new ElementSerializer();
            var source = new CustomElement(Guid.NewGuid(), ElemType.Location, "Castle", 42);
            var payload = new CustomSerializer().Serialize(source);
            Assert.Throws<NotSupportedException>(() => serializer.Serialize(source));
            Assert.Throws<MessagePack.MessagePackSerializationException>(() => serializer.Deserialize(payload));
            serializer.Register(new CustomSerializer());
            Assert.Throws<ArgumentException>(() => serializer.Register(new CustomSerializer()));
            Assert.Throws<ArgumentException>(() => serializer.Register(new DuplicateIdSerializer()));
            Assert.IsType<CustomElement>(serializer.Deserialize(payload));
        }

        [Fact]
        public void DispatcherRejectsTrailingBytes()
        {
            var serializer = new ElementSerializer();
            var bytes = serializer.Serialize(new Element(ElemType.Location));
            Array.Resize(ref bytes, bytes.Length + 1);
            Assert.Throws<MessagePack.MessagePackSerializationException>(() => serializer.Deserialize(bytes));
        }
        [Fact]
        public void DerivedSerializerPreservesCommonAndCustomFields()
        {
            var source = new CustomElement(Guid.NewGuid(), ElemType.Location, "citadel", 42);
            var key = new ParamKey<int>("Level", "world");
            source.Params.Add(key, 3);
            var serializer = new CustomSerializer();

            var restored = Assert.IsType<CustomElement>(serializer.Deserialize(serializer.Serialize(source)));
            Assert.Equal(source.Id, restored.Id);
            Assert.Equal(source.Name, restored.Name);
            Assert.Equal(42, restored.Danger);
            Assert.True(restored.Params.TryGet(key, out int value));
            Assert.Equal(3, value);
        }

        [Fact]
        public void WrongSerializerIsRejectedOnWriteAndRead()
        {
            var source = new CustomElement(Guid.NewGuid(), ElemType.Location, "citadel", 42);
            var configured = new CustomSerializer();
            var unconfigured = new ElementMessagePackSerializer();
            var payload = configured.Serialize(source);
            Assert.Throws<NotSupportedException>(() => unconfigured.Serialize(source));
            Assert.Throws<MessagePack.MessagePackSerializationException>(() => unconfigured.Deserialize(payload));
        }

        private sealed class CustomElement : IElement
        {
            public CustomElement(Guid id, ElemType type, string name, int danger)
            { Id = id; Type = type; Name = name; Danger = danger; }
            public Guid Id { get; }
            public ElemType Type { get; }
            public string Name { get; set; }
            public string Description { get; set; } = "";
            public int Time { get; set; } = -1;
            public ParamBag Params { get; } = new ParamBag();
            public int Danger { get; }
            public bool IsEmpty() => false;
        }

        private sealed class CustomSerializer : ElementMessagePackSerializerBase
        {
            protected override string ImplementationId => "test.custom.v1";
            protected override Type ElementType => typeof(CustomElement);
            protected override byte[] SerializeExtension(IElement element) => MessagePack.MessagePackSerializer.Serialize(((CustomElement)element).Danger);
            protected override IElement DeserializeExtension(ElementMessagePackData common, byte[] extension) =>
                new CustomElement(common.Id, common.Type, common.Name, MessagePack.MessagePackSerializer.Deserialize<int>(extension));
        }

        private sealed class DuplicateIdSerializer : ElementMessagePackSerializerBase
        {
            protected override string ImplementationId => "test.custom.v1";
            protected override Type ElementType => typeof(AlternateElement);
            protected override byte[] SerializeExtension(IElement element) => Array.Empty<byte>();
            protected override IElement DeserializeExtension(ElementMessagePackData common, byte[] extension) => throw new NotSupportedException();
        }

        private sealed class AlternateElement : Element
        {
            public AlternateElement() : base(ElemType.Location) { }
        }
        [Fact]
        public void RoundTripPreservesIdentityAndTypedParameters()
        {
            IElement source = new Element(ElemType.Location, "Замок", "Описание", 7);
            var population = new ParamKey<int>("Population", "world");
            var tags = new ParamKey<List<string>>("Tags", "world");
            source.Params.Add(population, 12);
            source.Params.Add(tags, new List<string> { "old", "stone" });

            var serializer = new ElementMessagePackSerializer();
            var restored = serializer.Deserialize(serializer.Serialize(source));

            Assert.Equal(source.Id, restored.Id);
            Assert.Equal(source.Type, restored.Type);
            Assert.Equal(source.Name, restored.Name);
            Assert.Equal(source.Description, restored.Description);
            Assert.Equal(source.Time, restored.Time);
            Assert.True(restored.Params.TryGet(population, out int actualPopulation));
            Assert.Equal(12, actualPopulation);
            Assert.True(restored.Params.TryGet(tags, out List<string> actualTags));
            Assert.Equal(new[] { "old", "stone" }, actualTags);
        }

        [Fact]
        public void DeserializeRejectsTrailingData()
        {
            var serializer = new ElementMessagePackSerializer();
            var bytes = serializer.Serialize(new Element(ElemType.Location));
            Array.Resize(ref bytes, bytes.Length + 1);
            Assert.ThrowsAny<Exception>(() => { serializer.Deserialize(bytes); });
        }
    }
}
