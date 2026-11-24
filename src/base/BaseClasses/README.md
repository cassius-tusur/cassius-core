# BaseClasses

`Plot` owns a collection of story elements and their typed relations. Element
identity is its immutable `Id`: content changes do not change identity, and a
plot rejects a distinct instance whose ID collides with an existing element.

## Standard relation routes

`Plot.Bind` materializes the reciprocal relation only for the source and target
types shown below. Standard keys reject unregistered type pairs. A registered
custom strategy takes precedence; unregistered custom keys remain one-sided.

| Forward key | Reciprocal key | Supported source → target | Direction |
| --- | --- | --- | --- |
| `Parent` | `Child` | `Character` → `Character` | Parent to child; multiple parents are permitted. |
| `Child` | `Parent` | `Character` → `Character` | Child to parent. |

Both keys are `ParamKey<bool>` values in `BaseRelationKeys`.
