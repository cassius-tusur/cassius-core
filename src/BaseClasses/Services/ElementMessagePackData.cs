using System;
using BaseClasses.Enum;
using BaseClasses.Interface;

namespace BaseClasses.Services
{
    /// <summary>Common immutable identity and fields needed to construct an implementation.</summary>
    public sealed class ElementMessagePackData
    {
        public Guid Id { get; }
        public ElemType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public int Time { get; }

        internal ElementMessagePackData(Guid id, ElemType type, string name, string description, int time)
        { Id = id; Type = type; Name = name; Description = description; Time = time; }
    }
}
