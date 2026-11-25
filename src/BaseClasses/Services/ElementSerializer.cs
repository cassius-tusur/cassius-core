using System;
using System.Collections.Generic;
using BaseClasses.Interface;
using MessagePack;

namespace BaseClasses.Services
{
    /// <summary>Selects the registered MessagePack serializer for each element implementation.</summary>
    public sealed class ElementSerializer
    {
        private readonly Dictionary<Type, ElementMessagePackSerializerBase> _byType = new Dictionary<Type, ElementMessagePackSerializerBase>();
        private readonly Dictionary<string, ElementMessagePackSerializerBase> _byId = new Dictionary<string, ElementMessagePackSerializerBase>(StringComparer.Ordinal);

        public ElementSerializer() => Register(new ElementMessagePackSerializer());

        public void Register(ElementMessagePackSerializerBase serializer)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            var id = serializer.RegisteredId;
            var type = serializer.RegisteredType;
            if (string.IsNullOrWhiteSpace(id) || type == null || !typeof(IElement).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                throw new ArgumentException("Serializer must specify a non-empty identifier and a concrete IElement type.", nameof(serializer));
            if (_byId.ContainsKey(id) || _byType.ContainsKey(type))
                throw new ArgumentException("A serializer for this identifier or element type is already registered.", nameof(serializer));
            _byId.Add(id, serializer);
            _byType.Add(type, serializer);
        }

        public byte[] Serialize(IElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (!_byType.TryGetValue(element.GetType(), out var serializer))
                throw new NotSupportedException($"No serializer registered for '{element.GetType()}'.");
            return serializer.Serialize(element);
        }

        public IElement Deserialize(byte[] payload)
        {
            var dto = ElementMessagePackSerializerBase.ReadWire(payload);
            if (!_byId.TryGetValue(dto.Implementation, out var serializer))
                throw new MessagePackSerializationException($"No serializer registered for '{dto.Implementation}'.");
            return serializer.DeserializeWire(dto);
        }
    }
}
