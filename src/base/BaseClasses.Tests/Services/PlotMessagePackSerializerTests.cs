using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services;
using BaseClasses.Services.Binds;
using MessagePack;
using Xunit;

namespace BaseClasses.Tests.Services
{
    public class PlotMessagePackSerializerTests
    {
        [Fact]
        public void PlotRoundTrip_PreservesElementsRelationsAndTypedParameters()
        {
            var plot = new Plot();
            var parent = new Element(ElemType.Character, "parent");
            var child = new Element(ElemType.Character, "child");
            plot.Add(parent); plot.Add(child);
            var id = Guid.NewGuid();
            var key = new ParamKey<Dictionary<string, object>>("Data");
            parent.Params.Add(key, new Dictionary<string, object> {
                ["id"] = id, ["price"] = 0.1234567890123456789012345678m,
                ["nested"] = new List<object> { 1L, new Dictionary<string, object> { ["id"] = id } }
            });
            plot.Bind(parent, child, BaseRelationKeys.Parent, true);
            plot.Time = 42;
            var serializer = new PlotMessagePackSerializer();
            var restored = serializer.Deserialize(serializer.Serialize(plot));
            Assert.Equal(42, restored.Time);
            Assert.Equal(2, restored.Elements.Count);
            var restoredParent = restored.Elements.Single(e => e.Id == parent.Id);
            var restoredChild = restored.Elements.Single(e => e.Id == child.Id);
            Assert.Equal("parent", restoredParent.Name);
            Assert.Equal(2, restored.Relations.Count);
            Assert.Contains(restored.Relations, r => ReferenceEquals(r.Source, restoredParent) && ReferenceEquals(r.Target, restoredChild) && r.Param.Equals(BaseRelationKeys.Parent));
            Assert.True(restoredParent.Params.TryGet(key, out var values));
            Assert.Equal(id, Assert.IsType<Guid>(values["id"]));
            Assert.Equal(0.1234567890123456789012345678m, Assert.IsType<decimal>(values["price"]));
            var list = Assert.IsType<List<object>>(values["nested"]);
            Assert.Equal(1L, Assert.IsType<long>(list[0]));
            Assert.Equal(id, Assert.IsType<Guid>(Assert.IsType<Dictionary<string, object>>(list[1])["id"]));
        }

        [Fact]
        public void PlotRoundTrip_PreservesUnassignedElementTimeAndCustomRelationValue()
        {
            var plot = new Plot { Time = 17 };
            var element = new Element(ElemType.Character, "untimed");
            plot.Elements.Add(element);
            var relationKey = new ParamKey<object>("value");
            var id = Guid.NewGuid();
            plot.Bind(element, element, relationKey, id);
            var serializer = new PlotMessagePackSerializer();
            var result = serializer.Deserialize(serializer.Serialize(plot));
            Assert.Equal(17, result.Time);
            Assert.Equal(-1, Assert.Single(result.Elements).Time);
            Assert.Equal(id, Assert.IsType<Guid>(Assert.Single(result.Relations).Value));
        }

        [Fact]
        public void PlotDeserialize_RejectsTrailingData()
        {
            var serializer = new PlotMessagePackSerializer();
            var bytes = serializer.Serialize(new Plot());
            Array.Resize(ref bytes, bytes.Length + 1);
            Assert.Throws<MessagePackSerializationException>(() => serializer.Deserialize(bytes));
        }

        [Fact]
        public void PlotRoundTrip_UsesRegisteredElementSerializerAndProvidedBinder()
        {
            var dispatcher = new ElementSerializer();
            dispatcher.Register(new CustomElementSerializer());
            var serializer = new PlotMessagePackSerializer(dispatcher);
            var plot = new Plot();
            var custom = new CustomElement(Guid.NewGuid(), 23);
            plot.Add(custom);
            var binder = new Binder();
            var restored = serializer.Deserialize(serializer.Serialize(plot), binder);
            var result = Assert.IsType<CustomElement>(Assert.Single(restored.Elements));
            Assert.Equal(custom.Id, result.Id);
            Assert.Equal(23, result.Extra);
            Assert.Same(binder, restored.Binder);
        }

        [Fact]
        public void PlotDeserialize_RejectsMissingRelationEndpoint()
        {
            var dto = new PlotMessagePackSerializer.PlotWire();
            dto.Relations.Add(new PlotMessagePackSerializer.RelationWire {
                Source = Guid.NewGuid(), Target = Guid.NewGuid(), Name = "X", Type = "bool",
                Value = MessagePackSerializer.Serialize(true)
            });
            var bytes = MessagePackSerializer.Serialize(dto);
            Assert.Throws<MessagePackSerializationException>(() => new PlotMessagePackSerializer().Deserialize(bytes));
        }

        private sealed class CustomElement : Element
        {
            public CustomElement(Guid id, int extra) : base(id, ElemType.Location, "custom", "", -1) => Extra = extra;
            public int Extra { get; }
        }

        private sealed class CustomElementSerializer : ElementMessagePackSerializerBase
        {
            protected override string ImplementationId => "test.custom.plot";
            protected override Type ElementType => typeof(CustomElement);
            protected override byte[] SerializeExtension(IElement element) => MessagePackSerializer.Serialize(((CustomElement)element).Extra);
            protected override IElement DeserializeExtension(ElementMessagePackData common, byte[] extension) =>
                new CustomElement(common.Id, MessagePackSerializer.Deserialize<int>(extension));
        }
    }
}
