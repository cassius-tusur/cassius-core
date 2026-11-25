using System;
using System.Reflection;
using BaseClasses.Enum;
using BaseClasses.Model;
using Xunit;

namespace BaseClasses.Tests.Model
{
    public class ElementTests
    {
        [Fact]
        public void Equals_ElementsWithIdenticalContentButDifferentIds_ReturnsFalse()
        {
            var first = new Element(ElemType.Character, "Hero", "Main", time: 3);
            var second = new Element(ElemType.Character, "Hero", "Main", time: 3);

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Equals_ElementsWithSameFixedId_ReturnsTrue()
        {
            var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var first = new Element(ElemType.Character, "First", time: 1);
            var second = new Element(ElemType.Location, "Second", time: 2);
            SetId(first, id);
            SetId(second, id);

            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Equals_ElementsWithSameIdAcrossSubclasses_ReturnsTrue()
        {
            var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var element = new Element(ElemType.Character, "Base");
            var derivedElement = new DerivedElement(ElemType.Location, "Derived");
            SetId(element, id);
            SetId(derivedElement, id);

            Assert.True(((object)element).Equals(derivedElement));
            Assert.True(((object)derivedElement).Equals(element));
        }

        private static void SetId(Element element, Guid id)
        {
            var idField = typeof(Element).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(idField);
            idField!.SetValue(element, id);
        }

        private sealed class DerivedElement : Element
        {
            public DerivedElement(ElemType type, string name)
                : base(type, name)
            {
            }
        }
    }
}
