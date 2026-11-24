using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses.Interface;
using BaseClasses.Services;
using BaseClasses.Services.Binds;

namespace BaseClasses.Model
{
    /// <summary>
    /// История.
    /// </summary>
    public class Plot
    {
        private readonly Action<IElement, IElement, Plot, bool> _mergeAction;

        /// <summary>
        /// Элементы истории.
        /// </summary>
        public HashSet<IElement> Elements { get; } = new HashSet<IElement>(ElementIdComparer.Instance);

        private sealed class ElementIdComparer : IEqualityComparer<IElement>
        {
            public static readonly ElementIdComparer Instance = new ElementIdComparer();
            public bool Equals(IElement? x, IElement? y) => ReferenceEquals(x, y) ||
                (x != null && y != null && x.Id == y.Id);
            public int GetHashCode(IElement element) => element.Id.GetHashCode();
        }

        /// <summary>
        /// Связи между элементами истории.
        /// </summary>
        public HashSet<Relation> Relations { get; } = new HashSet<Relation>();

        /// <summary>
        /// Сервис для связывания элементов истории.
        /// </summary>
        public Binder Binder { get; }

        /// <summary>
        /// Время.
        /// </summary>
        public int Time { get; set; }

        /// <summary>
        /// Создание истории.
        /// </summary>
        /// <param name="binder">Пользовательский binder для тестов/кастомной логики.</param>
        /// <param name="mergeAction">Пользовательская логика объединения элементов.</param>
        public Plot(Binder? binder = null, Action<IElement, IElement, Plot, bool>? mergeAction = null)
        {
            Binder = binder ?? new Binder();
            _mergeAction = mergeAction ?? (Merger.Merge);
        }

        /// <summary>
        /// Добавление элемента в историю.
        /// </summary>
        /// <param name="element">Элемент.</param>
        public void Add(IElement element)
        {
            var existingElement = Elements.FirstOrDefault(candidate => candidate.Id == element.Id);
            if (existingElement != null)
            {
                if (ReferenceEquals(existingElement, element)) return;
                throw new InvalidOperationException($"An element with id '{element.Id}' is already part of the plot.");
            }

            if (element.Time == -1) element.Time = Time++;
            Elements.Add(element);
        }

        /// <summary>
        /// Удаление элемента из истории.
        /// </summary>
        /// <param name="element">Удаляемый элемент.</param>
        public void Remove(IElement element)
        {
            if (!Elements.TryGetValue(element, out var existingElement)) return;
            foreach (var r in Relations.Where(r => r.Source.Id == existingElement.Id
                                                   || r.Target.Id == existingElement.Id).ToList())
            {
                Binder.Unbind(r.Source, r.Target, r.Param, this);
            }
            Elements.Remove(existingElement);
        }

        /// <summary>
        /// Связывание элементов истории через параметр.
        /// </summary>
        /// <param name="source">Источник связи.</param>
        /// <param name="target">Приемник связи.</param>
        /// <param name="paramKey">Параметр, описывающий связь.</param>
        /// <param name="value">Значение параметра, описывающее связь.</param>
        /// <exception cref="KeyNotFoundException">Выбрасывается, если источник или приемник связи
        /// не являются частью истории.</exception>
        /// <exception cref="ArgumentException">Выбрасывается, если значение не соответствует типу,
        /// определенному в paramKey.</exception>
        public void Bind(IElement source, IElement target, IParamKey paramKey, object? value)
        {
            source = Elements.FirstOrDefault(element => element.Id == source.Id)
                ?? throw new KeyNotFoundException($"Source elements ({source}) must be part of the plot.");
            target = Elements.FirstOrDefault(element => element.Id == target.Id)
                ?? throw new KeyNotFoundException($"Target elements ({target}) must be part of the plot.");
            if (!paramKey.IsTypeMatch(value))
                throw new ArgumentException($"Value {value} does not match the type defined in paramKey {paramKey}.");
            Binder.Bind(source, target, paramKey, value, this);
        }

        /// <summary>
        /// Удаление связи между элементами истории, описанной параметром.
        /// </summary>
        /// <param name="source">Источник связи.</param>
        /// <param name="target">Приемник связи.</param>
        /// <param name="paramKey">Параметр, описывающий связь.</param>
        /// <exception cref="KeyNotFoundException">Выбрасывается, если источник или приемник
        /// связи не являются частью истории.</exception>
        public void Unbind(IElement source, IElement target, IParamKey paramKey)
        {
            source = Elements.FirstOrDefault(element => element.Id == source.Id)
                ?? throw new KeyNotFoundException($"Source elements ({source}) must be part of the plot.");
            target = Elements.FirstOrDefault(element => element.Id == target.Id)
                ?? throw new KeyNotFoundException($"Target elements ({target}) must be part of the plot.");
            Binder.Unbind(source, target, paramKey, this);
        }

        /// <summary>
        /// Объединение двух элементов истории, с сохранением связей и приоритетом одного из элементов.
        /// </summary>
        /// <param name="baseElement">Базовый элемент, объект которого сохранится.</param>
        /// <param name="targetElement">Объединяемый элемент, объект которого будет удален.</param>
        /// <param name="basePriority">Приоритет базового элемента. Если True, то при конфликте
        /// сохраняются данные базового элемента, иначе - объединяемого.</param>
        public void Merge(IElement baseElement, IElement targetElement, bool basePriority = true)
        {
            Add(baseElement);
            _mergeAction(baseElement, targetElement, this, basePriority);
        }
    }
}
