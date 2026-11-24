using System;
using System.Collections.Generic;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using MessagePack;

namespace BaseClasses.Services
{
    /// <summary>Serializes common IElement fields; subclasses handle implementation-specific data.</summary>
    public abstract class ElementMessagePackSerializerBase
    {
        private static readonly MessagePackSerializerOptions Options = MessagePackValueCodec.Options;
        protected abstract string ImplementationId { get; }
        protected abstract Type ElementType { get; }
        protected abstract byte[] SerializeExtension(IElement element);
        protected abstract IElement DeserializeExtension(ElementMessagePackData common, byte[] extension);
        internal string RegisteredId => ImplementationId;
        internal Type RegisteredType => ElementType;

        public byte[] Serialize(IElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (element.GetType() != ElementType)
                throw new NotSupportedException($"This serializer does not support '{element.GetType()}'.");

            var dto = new ElementWire
            {
                Id = element.Id, Type = (int)element.Type,
                Name = element.Name, Description = element.Description, Time = element.Time,
                Implementation = ImplementationId, Extension = SerializeExtension(element) ??
                    throw new MessagePackSerializationException("Serializer returned null extension.")
            };
            foreach (var pair in element.Params.Enumerate())
            {
                var typeName = MessagePackValueCodec.TypeName(pair.Key.ValueType);
                if (!pair.Key.TryConvertValue(pair.Value, out var value))
                    throw new MessagePackSerializationException($"Parameter '{pair.Key.Name}' cannot be converted to '{pair.Key.ValueType}'.");
                dto.Parameters.Add(new ParameterWire
                {
                    Namespace = pair.Key.Namespace, Name = pair.Key.Name,
                    Type = typeName,
                    Value = MessagePackValueCodec.Serialize(pair.Key.ValueType, value, Options)
                });
            }
            return MessagePackSerializer.Serialize(dto, Options);
        }

        public IElement Deserialize(byte[] payload)
        {
            return DeserializeWire(ReadWire(payload));
        }

        internal static ElementWire ReadWire(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var dto = MessagePackSerializer.Deserialize<ElementWire>(payload, Options, out var bytesRead);
            if (bytesRead != payload.Length) throw new MessagePackSerializationException("Trailing data after element.");
            if (dto == null || dto.Id == Guid.Empty || dto.Parameters == null || dto.Name == null || dto.Description == null ||
                dto.Implementation == null || dto.Extension == null ||
                !System.Enum.IsDefined(typeof(ElemType), dto.Type))
                throw new MessagePackSerializationException("Invalid element.");
            return dto;
        }

        internal IElement DeserializeWire(ElementWire dto)
        {
            if (!StringComparer.Ordinal.Equals(dto.Implementation, ImplementationId))
                throw new MessagePackSerializationException($"Unexpected element implementation '{dto.Implementation}'.");
            var common = new ElementMessagePackData(dto.Id, (ElemType)dto.Type, dto.Name, dto.Description, dto.Time);
            var element = DeserializeExtension(common, dto.Extension);
            if (element == null || element.GetType() != ElementType || element.Id != dto.Id || element.Type != common.Type || element.Params.Count != 0)
                throw new MessagePackSerializationException("Serializer returned an element with invalid identity, type or initial parameters.");
            element.Name = dto.Name;
            element.Description = dto.Description;
            element.Time = dto.Time;
            foreach (var entry in dto.Parameters)
            {
                if (entry == null || entry.Name == null || entry.Namespace == null || entry.Type == null || entry.Value == null)
                    throw new MessagePackSerializationException("Invalid parameter.");
                var type = TypeNameHelper.ResolveType(entry.Type);
                var key = (IParamKey)Activator.CreateInstance(typeof(ParamKey<>).MakeGenericType(type), entry.Name, entry.Namespace)!;
                var value = MessagePackValueCodec.Deserialize(type, entry.Value, Options);
                element.Params.Add(key, value);
            }
            return element;
        }

        [MessagePackObject]
        public sealed class ElementWire
        {
            [Key(0)] public Guid Id { get; set; }
            [Key(1)] public int Type { get; set; }
            [Key(2)] public string Name { get; set; } = "";
            [Key(3)] public string Description { get; set; } = "";
            [Key(4)] public int Time { get; set; }
            [Key(5)] public List<ParameterWire> Parameters { get; set; } = new List<ParameterWire>();
            [Key(6)] public string Implementation { get; set; } = "";
            [Key(7)] public byte[] Extension { get; set; } = Array.Empty<byte>();
        }

        [MessagePackObject]
        public sealed class ParameterWire
        {
            [Key(0)] public string Namespace { get; set; } = "";
            [Key(1)] public string Name { get; set; } = "";
            [Key(2)] public string Type { get; set; } = "";
            [Key(3)] public byte[] Value { get; set; } = Array.Empty<byte>();
        }

    }

    /// <summary>MessagePack serializer for the standard Element implementation.</summary>
    public sealed class ElementMessagePackSerializer : ElementMessagePackSerializerBase
    {
        protected override string ImplementationId => "cassius.element";
        protected override Type ElementType => typeof(Element);
        protected override byte[] SerializeExtension(IElement element) => Array.Empty<byte>();
        protected override IElement DeserializeExtension(ElementMessagePackData common, byte[] extension)
        {
            if (extension.Length != 0) throw new MessagePackSerializationException("Unexpected Element extension.");
            return new Element(common.Id, common.Type, common.Name, common.Description, common.Time);
        }
    }
}
