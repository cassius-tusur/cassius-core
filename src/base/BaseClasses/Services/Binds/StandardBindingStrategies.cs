using System.Collections.Generic;
using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Model;

namespace BaseClasses.Services.Binds
{
    public static class StandardBindingStrategies
    {
        public static readonly RelationBindingStrategy BindOneSide = (source, target, key, value, plot) =>
        {
            var rel = plot.Relations.FirstOrDefault(r =>
                r.Source.Id == source.Id && r.Target.Id == target.Id && r.Param.Equals(key));
            if (rel is null)
                plot.Relations.Add(new Relation(source, target, key, value));
            else
                rel.Value = value;
        };
        
        public static readonly RelationBindingStrategy UnbindOneSide = (source, target, key, _, plot) =>
        {
            var rel = plot.Relations.FirstOrDefault(r =>
                r.Source.Id == source.Id && r.Target.Id == target.Id && r.Param.Equals(key));
            if (rel is null) return;
            plot.Relations.Remove(rel);
        };
        
        public static readonly RelationBindingStrategy BindCharacters = (source, target, key, value, plot) =>
        {
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, key, value, plot);
        };

        public static readonly RelationBindingStrategy UnbindCharacters = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, key, value, plot);
        };

        public static readonly RelationBindingStrategy BindCharItem = (source, target, key, value, plot) =>
        {
            var other = plot.Relations.FirstOrDefault(r =>
                    r.Source.Id == target.Id && r.Param.Equals(BaseRelationKeys.Owned))
                ?.Target ?? plot.Relations.FirstOrDefault(r =>
                    r.Target.Id == target.Id && r.Param.Equals(BaseRelationKeys.Owns))
                ?.Source;
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, BaseRelationKeys.Owned, value, plot);
            if (!(other is null))
            {
                UnbindCharItem?.Invoke(other, target, key, null, plot);
            }
        };

        public static readonly RelationBindingStrategy UnbindCharItem = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, BaseRelationKeys.Owned, value, plot);
        };

        public static readonly RelationBindingStrategy BindItemChar = (source, target, _, value, plot) =>
        {
            BindCharItem(target, source, BaseRelationKeys.Owns, value, plot);
        };
        
        public static readonly RelationBindingStrategy UnbindItemChar = (source, target, _, value, plot) =>
        {
            UnbindCharItem(target, source, BaseRelationKeys.Owns, value, plot);
        };

        public static readonly RelationBindingStrategy BindCharLoc = (source, target, key, value, plot) =>
        {
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, BaseRelationKeys.Locates, value, plot);
        };

        public static readonly RelationBindingStrategy UnbindCharLoc = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, BaseRelationKeys.Locates, value, plot);
        };
        
        public static readonly RelationBindingStrategy BindLocChar = (source, target, _, value, plot) =>
        {
            BindCharLoc(target, source, BaseRelationKeys.Located, value, plot);
        };
        
        public static readonly RelationBindingStrategy UnbindLocChar = (source, target, _, value, plot) =>
        {
            UnbindCharLoc(target, source, BaseRelationKeys.Located, value, plot);
        };

        public static readonly RelationBindingStrategy BindCharEvent = (source, target, key, value, plot) =>
        {
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, BaseRelationKeys.Involves, value, plot);
        };
        
        public static readonly RelationBindingStrategy UnbindCharEvent = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, BaseRelationKeys.Involves, value, plot);
        };
        
        public static readonly RelationBindingStrategy BindEventChar = (source, target, _, value, plot) =>
        {
            BindCharEvent(target, source, BaseRelationKeys.Participates, value, plot);
        };

        public static readonly RelationBindingStrategy UnbindEventChar = (source, target, _, value, plot) =>
        {
            UnbindCharEvent(target, source, BaseRelationKeys.Participates, value, plot);
        };

        public static readonly RelationBindingStrategy BindItemLoc = (source, target, key, value, plot) =>
        {
            var other = plot.Relations.FirstOrDefault(r =>
                    r.Source.Id == source.Id && r.Param.Equals(BaseRelationKeys.Located))
                ?.Target ?? plot.Relations.FirstOrDefault(r =>
                    r.Target.Id == source.Id && r.Param.Equals(BaseRelationKeys.Locates))
                ?.Source;
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, BaseRelationKeys.Locates, value, plot);
            if (!(other is null))
            {
                UnbindItemLoc?.Invoke(source, other, BaseRelationKeys.Located, null, plot);
            }
        };

        public static readonly RelationBindingStrategy UnbindItemLoc = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, BaseRelationKeys.Locates, value, plot);
        };
        
        public static readonly RelationBindingStrategy BindLocItem = (source, target, _, value, plot) =>
        {
            BindItemLoc(target, source, BaseRelationKeys.Located, value, plot);
        };
        
        public static readonly RelationBindingStrategy UnbindLocItem = (source, target, _, value, plot) =>
        {
            UnbindItemLoc(target, source, BaseRelationKeys.Located, value, plot);
        };

        public static readonly RelationBindingStrategy BindItemEvent = (source, target, key, value, plot) =>
        {
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, BaseRelationKeys.Uses, value, plot);
        };
        
        public static readonly RelationBindingStrategy UnbindItemEvent = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, BaseRelationKeys.Uses, value, plot);
        };
        
        public static readonly RelationBindingStrategy BindEventItem = (source, target, _, value, plot) =>
        {
            BindItemEvent(target, source, BaseRelationKeys.Used, value, plot);
        };
        
        public static readonly RelationBindingStrategy UnbindEventItem = (source, target, _, value, plot) =>
        {
            UnbindItemEvent(target, source, BaseRelationKeys.Used, value, plot);
        };

        public static readonly RelationBindingStrategy BindLocEvent = (source, target, key, value, plot) =>
        {
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, BaseRelationKeys.Located, value, plot);
        };
        
        public static readonly RelationBindingStrategy UnbindLocEvent = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, BaseRelationKeys.Located, value, plot);
        };
        
        public static readonly RelationBindingStrategy BindEventLoc = (source, target, _, value, plot) =>
        {
            BindLocEvent(target, source, BaseRelationKeys.Locates, value, plot);
        };
        
        public static readonly RelationBindingStrategy UnbindEventLoc = (source, target, _, value, plot) =>
        {
            UnbindLocEvent(target, source, BaseRelationKeys.Locates, value, plot);
        };

        public static readonly RelationBindingStrategy BindParentChild = (source, target, key, value, plot) =>
        {
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, BaseRelationKeys.Child, value, plot);
        };

        public static readonly RelationBindingStrategy UnbindParentChild = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, BaseRelationKeys.Child, value, plot);
        };

        public static readonly RelationBindingStrategy BindChildParent = (source, target, _, value, plot) =>
        {
            BindParentChild(target, source, BaseRelationKeys.Parent, value, plot);
        };

        public static readonly RelationBindingStrategy UnbindChildParent = (source, target, _, value, plot) =>
        {
            UnbindParentChild(target, source, BaseRelationKeys.Parent, value, plot);
        };

        public static readonly RelationBindingStrategy BindContains = (source, target, key, value, plot) =>
        {
            BindOneSide(source, target, key, value, plot);
            BindOneSide(target, source, BaseRelationKeys.ContainedBy, value, plot);
        };

        public static readonly RelationBindingStrategy UnbindContains = (source, target, key, value, plot) =>
        {
            UnbindOneSide(source, target, key, value, plot);
            UnbindOneSide(target, source, BaseRelationKeys.ContainedBy, value, plot);
        };

        public static readonly RelationBindingStrategy BindContainedBy = (source, target, _, value, plot) =>
        {
            BindContains(target, source, BaseRelationKeys.Contains, value, plot);
        };

        public static readonly RelationBindingStrategy UnbindContainedBy = (source, target, _, value, plot) =>
        {
            UnbindContains(target, source, BaseRelationKeys.Contains, value, plot);
        };

        public static readonly Dictionary<RelationRoute, (RelationBindingStrategy, RelationBindingStrategy)> Routes =
            new Dictionary<RelationRoute, (RelationBindingStrategy, RelationBindingStrategy)>
            {
                {
                    new RelationRoute(ElemType.Character, ElemType.Character, BaseRelationKeys.Relationship),
                    (BindCharacters, UnbindCharacters)
                },
                {
                    new RelationRoute(ElemType.Character, ElemType.Item, BaseRelationKeys.Owns),
                    (BindCharItem, UnbindCharItem)
                },
                {
                    new RelationRoute(ElemType.Character, ElemType.Location, BaseRelationKeys.Located),
                    (BindCharLoc, UnbindCharLoc)
                },
                {
                    new RelationRoute(ElemType.Character, ElemType.Event, BaseRelationKeys.Participates),
                    (BindCharEvent, UnbindCharEvent)
                },
                {
                    new RelationRoute(ElemType.Item, ElemType.Character, BaseRelationKeys.Owned),
                    (BindItemChar, UnbindItemChar)
                },
                {
                    new RelationRoute(ElemType.Item, ElemType.Location, BaseRelationKeys.Located),
                    (BindItemLoc, UnbindItemLoc)
                },
                {
                    new RelationRoute(ElemType.Item, ElemType.Event, BaseRelationKeys.Used),
                    (BindItemEvent, UnbindItemEvent)
                },
                {
                    new RelationRoute(ElemType.Location, ElemType.Character, BaseRelationKeys.Locates),
                    (BindLocChar, UnbindLocChar)
                },
                {
                    new RelationRoute(ElemType.Location, ElemType.Item, BaseRelationKeys.Locates),
                    (BindLocItem, UnbindLocItem)
                },
                {
                    new RelationRoute(ElemType.Location, ElemType.Event, BaseRelationKeys.Locates),
                    (BindLocEvent, UnbindLocEvent)
                },
                {
                    new RelationRoute(ElemType.Event, ElemType.Character, BaseRelationKeys.Involves),
                    (BindEventChar, UnbindEventChar)
                },
                {
                    new RelationRoute(ElemType.Event, ElemType.Item, BaseRelationKeys.Uses),
                    (BindEventItem, UnbindEventItem)
                },
                {
                    new RelationRoute(ElemType.Event, ElemType.Location, BaseRelationKeys.Located),
                    (BindEventLoc, UnbindEventLoc)
                },
                {
                    new RelationRoute(ElemType.Character, ElemType.Character, BaseRelationKeys.Parent),
                    (BindParentChild, UnbindParentChild)
                },
                {
                    new RelationRoute(ElemType.Character, ElemType.Character, BaseRelationKeys.Child),
                    (BindChildParent, UnbindChildParent)
                },
                {
                    new RelationRoute(ElemType.Location, ElemType.Location, BaseRelationKeys.Contains),
                    (BindContains, UnbindContains)
                },
                {
                    new RelationRoute(ElemType.Location, ElemType.Location, BaseRelationKeys.ContainedBy),
                    (BindContainedBy, UnbindContainedBy)
                },
                {
                    new RelationRoute(ElemType.Location, ElemType.Location, BaseRelationKeys.Passage),
                    (BindCharacters, UnbindCharacters)
                }
            };
    }
}
