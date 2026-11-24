using System;
using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services.Binds;
using BaseClasses.Services;
using Xunit;

namespace BaseClasses.Tests.Model
{
    /// <summary>
    /// Тесты для класса Plot.
    /// </summary>
    public class PlotTests
    {
        [Fact]
        public void Remove_CustomElementCopy_RemovesCanonicalElementAndRelations()
        {
            var plot = new Plot();
            var source = new FixedIdElement(Guid.NewGuid(), ElemType.Character, "source");
            var target = new FixedIdElement(Guid.NewGuid(), ElemType.Character, "target");
            plot.Add(source);
            plot.Add(target);
            plot.Bind(source, target, new ParamKey<bool>("custom"), true);
            plot.Remove(new FixedIdElement(source.Id, source.Type, source.Name));
            Assert.Single(plot.Elements);
            Assert.Same(target, plot.Elements.Single());
            Assert.Empty(plot.Relations);
        }

        [Fact]
        public void Add_CustomElementsWithValueEquality_UsesIds()
        {
            var plot = new Plot();
            plot.Add(new ValueEqualElement(Guid.NewGuid()));
            plot.Add(new ValueEqualElement(Guid.NewGuid()));
            Assert.Equal(2, plot.Elements.Count);
        }

        [Fact]
        public void Bind_CustomElementsWithValueEquality_KeepsDistinctRelations()
        {
            var plot = new Plot();
            var first = new ValueEqualElement(Guid.NewGuid());
            var second = new ValueEqualElement(Guid.NewGuid());
            var target = new ValueEqualElement(Guid.NewGuid());
            plot.Add(first);
            plot.Add(second);
            plot.Add(target);
            var key = new ParamKey<bool>("custom");
            plot.Bind(first, target, key, true);
            plot.Bind(second, target, key, true);
            Assert.Equal(2, plot.Relations.Count);
            plot.Unbind(first, target, key);
            Assert.Same(second, Assert.Single(plot.Relations).Source);
        }

        private sealed class ValueEqualElement : IElement
        {
            public ValueEqualElement(Guid id) { Id = id; }
            public Guid Id { get; }
            public ElemType Type => ElemType.Character;
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public ParamBag Params { get; } = new ParamBag();
            public int Time { get; set; } = -1;
            public bool IsEmpty() => false;
            public override bool Equals(object? other) => other is ValueEqualElement;
            public override int GetHashCode() => 0;
        }
        [Fact]
        public void Bind_EquivalentInstances_UsesInstancesOwnedByPlot()
        {
            var plot = new Plot();
            var source = new Element(ElemType.Character);
            var target = new Element(ElemType.Character);
            plot.Add(source);
            plot.Add(target);
            var serializer = new ElementMessagePackSerializer();
            var sourceCopy = serializer.Deserialize(serializer.Serialize(source));
            var targetCopy = serializer.Deserialize(serializer.Serialize(target));
            plot.Bind(sourceCopy, targetCopy, new ParamKey<bool>("custom"), true);
            var relation = Assert.Single(plot.Relations);
            Assert.Same(source, relation.Source);
            Assert.Same(target, relation.Target);
        }

        [Fact]
        public void Add_SameInstanceTwice_IsIdempotent()
        {
            var plot = new Plot();
            var element = new Element(ElemType.Character, "Hero");

            plot.Add(element);
            plot.Add(element);

            Assert.Single(plot.Elements);
            Assert.Contains(element, plot.Elements);
        }

        [Fact]
        public void Add_DistinctInstancesWithSameId_ThrowsInvalidOperationException()
        {
            var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var plot = new Plot();
            var first = new FixedIdElement(id, ElemType.Character, "First");
            var second = new FixedIdElement(id, ElemType.Location, "Second");
            plot.Add(first);

            Assert.Throws<InvalidOperationException>(() => plot.Add(second));
            Assert.Single(plot.Elements);
            Assert.Contains(first, plot.Elements);
        }

        /// <summary>
        /// Проверяет, что метод Bind использует зарегистрированную стратегию связывания из Binder.
        /// </summary>
        [Fact]
        public void Plot_Bind_UsesBinderMockStrategy()
        {
            var binderMock = new Binder();
            var bindCalls = 0;
            binderMock.Register(
                ElemType.Character,
                ElemType.Character,
                BaseRelationKeys.Relationship,
                (source, target, key, value, plot) =>
                {
                    bindCalls++;
                    plot.Relations.Add(new Relation(source, target, key, value));
                },
                (source, target, key, value, plot) => { });

            var plot = new Plot(binderMock);
            var source = new Element(ElemType.Character, "A");
            var target = new Element(ElemType.Character, "B");
            plot.Add(source);
            plot.Add(target);

            plot.Bind(source, target, BaseRelationKeys.Relationship, 0.8d);

            Assert.Equal(1, bindCalls);
            Assert.Single(plot.Relations);
            Assert.Equal(0.8d, plot.Relations.First().Value);
        }

        /// <summary>
        /// Проверяет, что метод Unbind использует зарегистрированную стратегию отвязывания из Binder
        /// и удаляет соответствующие отношения из истории.
        /// </summary>
        [Fact]
        public void Plot_Unbind_UsesBinderMockStrategy()
        {
            var binderMock = new Binder();
            var unbindCalls = 0;
            binderMock.Register(
                ElemType.Character,
                ElemType.Character,
                BaseRelationKeys.Relationship,
                (source, target, key, value, plot) =>
                {
                    plot.Relations.Add(new Relation(source, target, key, value));
                },
                (source, target, key, value, plot) =>
                {
                    unbindCalls++;
                    plot.Relations.RemoveWhere(r =>
                        r.Source.Equals(source) && r.Target.Equals(target) && r.Param.Equals(key));
                });

            var plot = new Plot(binderMock);
            var source = new Element(ElemType.Character, "A");
            var target = new Element(ElemType.Character, "B");
            plot.Add(source);
            plot.Add(target);
            plot.Bind(source, target, BaseRelationKeys.Relationship, 1.0d);

            plot.Unbind(source, target, BaseRelationKeys.Relationship);

            Assert.Equal(1, unbindCalls);
            Assert.Empty(plot.Relations);
        }

        /// <summary>
        /// Проверяет, что при удалении элемента из истории через метод Remove,
        /// все отношения, связанные с этим элементом, удаляются с помощью стратегии отвязывания из Binder,
        /// и элемент удаляется из списка элементов истории.
        /// </summary>
        [Fact]
        public void Plot_Remove_UnbindsAllRelationsViaBinderMock()
        {
            var binderMock = new Binder();
            var unbindCalls = 0;
            binderMock.Register(
                ElemType.Character,
                ElemType.Character,
                BaseRelationKeys.Relationship,
                (source, target, key, value, plot) =>
                {
                    plot.Relations.Add(new Relation(source, target, key, value));
                },
                (source, target, key, value, plot) =>
                {
                    unbindCalls++;
                    plot.Relations.RemoveWhere(r =>
                        r.Source.Equals(source) && r.Target.Equals(target) && r.Param.Equals(key));
                });

            var plot = new Plot(binderMock);
            var a = new Element(ElemType.Character, "A");
            var b = new Element(ElemType.Character, "B");
            var c = new Element(ElemType.Character, "C");
            plot.Add(a);
            plot.Add(b);
            plot.Add(c);

            plot.Bind(a, b, BaseRelationKeys.Relationship, 0.1d);
            plot.Bind(c, a, BaseRelationKeys.Relationship, 0.2d);

            plot.Remove(a);

            Assert.Equal(2, unbindCalls);
            Assert.DoesNotContain(a, plot.Elements);
            Assert.DoesNotContain(plot.Relations, r => r.Source.Equals(a) || r.Target.Equals(a));
        }

        /// <summary>
        /// Проверяет, что метод Merge использует предоставленную пользовательскую логику объединения (mergerMock),
        /// и что после объединения базовый элемент остается в истории, а целевой элемент удаляется.
        /// </summary>
        [Fact]
        public void Plot_Merge_UsesMergerMock_AndRemovesTarget()
        {
            var mergerCalls = 0;
            var baseElementIsInPlotOnMergeCall = false;
            var targetElementIsInPlotOnMergeCall = false;
            Action<IElement, IElement, Plot, bool> mergerMock = (baseElement, targetElement, plot, basePriority) =>
            {
                mergerCalls++;
                
                baseElementIsInPlotOnMergeCall = plot.Elements.Contains(baseElement);
                targetElementIsInPlotOnMergeCall = plot.Elements.Contains(targetElement);
            };

            var plot = new Plot(mergeAction: mergerMock);
            var baseElement = new Element(ElemType.Character, "Base");
            var targetElement = new Element(ElemType.Character, "Target");

            plot.Merge(baseElement, targetElement, false);

            Assert.Equal(1, mergerCalls);
            Assert.True(baseElementIsInPlotOnMergeCall);
            Assert.False(targetElementIsInPlotOnMergeCall);
            Assert.Contains(baseElement, plot.Elements);
            Assert.DoesNotContain(targetElement, plot.Elements);
        }

        private sealed class FixedIdElement : IElement
        {
            public FixedIdElement(Guid id, ElemType type, string name)
            {
                Id = id;
                Type = type;
                Name = name;
                Params = new ParamBag();
            }

            public Guid Id { get; }
            public ElemType Type { get; }
            public string Name { get; set; }
            public string Description { get; set; } = string.Empty;
            public ParamBag Params { get; }
            public int Time { get; set; } = -1;
            public bool IsEmpty() => false;
        }
    }
}
