# BaseClasses

`Plot` owns a collection of story elements and their typed relations. Element
identity is its immutable `Id`: content changes do not change identity, and a
plot rejects a distinct instance whose ID collides with an existing element.

## Standard relation routes

`Plot.Bind` materializes the reciprocal relation only for the source and target
types shown below. Unsupported type pairs remain one-sided custom relations.

| Forward key | Reciprocal key | Supported source → target | Direction |
| --- | --- | --- | --- |
| `Parent` | `Child` | `Character` → `Character` | Parent to child; multiple parents are permitted. |
| `Child` | `Parent` | `Character` → `Character` | Child to parent. |
| `Contains` | `ContainedBy` | `Location` → `Location` | Container to contained location. |
| `ContainedBy` | `Contains` | `Location` → `Location` | Contained location to container. |
| `Passage` | `Passage` | `Location` ↔ `Location` | Symmetric location-to-location passage. |

All five keys are `ParamKey<bool>` values in `BaseRelationKeys`.
