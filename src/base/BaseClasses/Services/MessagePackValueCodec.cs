using System;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace BaseClasses.Services
{
    internal static class MessagePackValueCodec
    {
        internal static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.UntrustedData).WithResolver(
            CompositeResolver.Create(new IMessagePackFormatter[] { new TypedObjectFormatter() },
                new IFormatterResolver[] { StandardResolver.Instance }));

        internal static string TypeName(Type type)
        {
            var name = TypeNameHelper.GetTypeName(type);
            Type resolved;
            try { resolved = TypeNameHelper.ResolveType(name); }
            catch (Exception ex) when (ex is TypeLoadException || ex is ArgumentException)
            { throw new NotSupportedException($"Type '{type}' cannot be restored.", ex); }
            if (resolved != type) throw new NotSupportedException($"Type '{type}' cannot be restored unambiguously.");
            return name;
        }

        internal static byte[] Serialize(Type type, object? value, MessagePackSerializerOptions options) =>
            value == null ? MessagePackSerializer.Serialize<object?>(value: null, options: options) : MessagePackSerializer.Serialize(type, value, options);

        internal static object? Deserialize(Type type, byte[] payload, MessagePackSerializerOptions options)
        {
            var reader = new MessagePackReader(payload);
            var value = reader.TryReadNil() ? null : MessagePackSerializer.Deserialize(type, ref reader, options);
            if (!reader.End) throw new MessagePackSerializationException("Trailing data after value.");
            return value;
        }

        internal sealed class TypedObjectFormatter : IMessagePackFormatter<object?>
        {
            public void Serialize(ref MessagePackWriter writer, object? value, MessagePackSerializerOptions options)
            {
                if (value == null) { writer.WriteNil(); return; }
                var type = value.GetType();
                writer.WriteArrayHeader(2);
                writer.Write(TypeName(type));
                if (type == typeof(object)) writer.WriteNil();
                else MessagePackSerializer.Serialize(type, ref writer, value, options);
            }

            public object? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            {
                if (reader.TryReadNil()) return null;
                options.Security.DepthStep(ref reader);
                try
                {
                    if (reader.ReadArrayHeader() != 2) throw new MessagePackSerializationException("Invalid typed object.");
                    var name = reader.ReadString() ?? throw new MessagePackSerializationException("Missing object type.");
                    var type = TypeNameHelper.ResolveType(name);
                    if (type == typeof(object)) { if (!reader.TryReadNil()) throw new MessagePackSerializationException("Invalid object value."); return new object(); }
                    return MessagePackSerializer.Deserialize(type, ref reader, options)!;
                }
                finally { reader.Depth--; }
            }
        }
    }
}
