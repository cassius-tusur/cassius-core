using System;
using System.Collections.Generic;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services.Binds;
using MessagePack;

namespace BaseClasses.Services
{
    /// <summary>Serializes a plot, its elements, and its relations using MessagePack.</summary>
    public sealed class PlotMessagePackSerializer
    {
        private readonly ElementSerializer _elements;
        private static readonly MessagePackSerializerOptions Options = MessagePackValueCodec.Options;

        public PlotMessagePackSerializer(ElementSerializer? elements = null) => _elements = elements ?? new ElementSerializer();

        public byte[] Serialize(Plot plot)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            var dto = new PlotWire { Time = plot.Time };
            var ids = new HashSet<Guid>();
            foreach (var element in plot.Elements)
            {
                if (element.Id == Guid.Empty || !ids.Add(element.Id))
                    throw new MessagePackSerializationException("Plot contains an invalid or duplicate element ID.");
                dto.Elements.Add(_elements.Serialize(element));
            }
            foreach (var relation in plot.Relations)
            {
                if (!ids.Contains(relation.Source.Id) || !ids.Contains(relation.Target.Id))
                    throw new MessagePackSerializationException("Relation refers to an element outside the plot.");
                if (!relation.Param.TryConvertValue(relation.Value, out var value))
                    throw new MessagePackSerializationException("Relation value does not match its key.");
                dto.Relations.Add(new RelationWire
                {
                    Source = relation.Source.Id, Target = relation.Target.Id,
                    Namespace = relation.Param.Namespace, Name = relation.Param.Name,
                    Type = MessagePackValueCodec.TypeName(relation.Param.ValueType),
                    Value = MessagePackValueCodec.Serialize(relation.Param.ValueType, value, Options)
                });
            }
            return MessagePackSerializer.Serialize(dto, Options);
        }

        /// <param name="binder">Optional binder to use for subsequent edits; strategies are not serialized.</param>
        public Plot Deserialize(byte[] payload, Binder? binder = null)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var dto = MessagePackSerializer.Deserialize<PlotWire>(payload, Options, out var bytesRead);
            if (bytesRead != payload.Length || dto == null || dto.Elements == null || dto.Relations == null)
                throw new MessagePackSerializationException("Invalid plot or trailing data.");
            var plot = new Plot(binder) { Time = dto.Time };
            var byId = new Dictionary<Guid, IElement>();
            foreach (var data in dto.Elements)
            {
                if (data == null) throw new MessagePackSerializationException("Invalid element payload.");
                var element = _elements.Deserialize(data);
                if (!byId.TryAdd(element.Id, element) || !plot.Elements.Add(element))
                    throw new MessagePackSerializationException("Duplicate element ID in plot.");
            }
            foreach (var relation in dto.Relations)
            {
                if (relation == null || relation.Name == null || relation.Namespace == null || relation.Type == null || relation.Value == null ||
                    !byId.TryGetValue(relation.Source, out var source) || !byId.TryGetValue(relation.Target, out var target))
                    throw new MessagePackSerializationException("Invalid relation or missing element.");
                var type = TypeNameHelper.ResolveType(relation.Type);
                var key = (IParamKey)Activator.CreateInstance(typeof(ParamKey<>).MakeGenericType(type), relation.Name, relation.Namespace)!;
                var value = MessagePackValueCodec.Deserialize(type, relation.Value, Options);
                if (!key.TryConvertValue(value, out var converted))
                    throw new MessagePackSerializationException("Relation value does not match its key.");
                if (!plot.Relations.Add(new Relation(source, target, key, converted)))
                    throw new MessagePackSerializationException("Duplicate relation in plot.");
            }
            return plot;
        }

        [MessagePackObject]
        public sealed class PlotWire
        {
            [Key(0)] public int Time { get; set; }
            [Key(1)] public List<byte[]> Elements { get; set; } = new List<byte[]>();
            [Key(2)] public List<RelationWire> Relations { get; set; } = new List<RelationWire>();
        }

        [MessagePackObject]
        public sealed class RelationWire
        {
            [Key(0)] public Guid Source { get; set; }
            [Key(1)] public Guid Target { get; set; }
            [Key(2)] public string Namespace { get; set; } = "";
            [Key(3)] public string Name { get; set; } = "";
            [Key(4)] public string Type { get; set; } = "";
            [Key(5)] public byte[] Value { get; set; } = Array.Empty<byte>();
        }
    }
}
