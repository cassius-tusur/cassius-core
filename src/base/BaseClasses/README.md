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
# MessagePack для элемента

`ElementMessagePackSerializer` сохраняет общие поля `IElement`: GUID, тип, название, описание, время и типизированные параметры. Связи `Plot` в этот формат не входят.

```csharp
var serializer = new ElementMessagePackSerializer();
byte[] data = serializer.Serialize(element);
IElement restored = serializer.Deserialize(data);
```

Для другой реализации или подкласса создайте наследника `ElementMessagePackSerializerBase`: задайте стабильный уникальный `ImplementationId` и `ElementType`, сохраните дополнительные поля в `SerializeExtension` и восстановите экземпляр в `DeserializeExtension`. Конструктор экземпляра должен восстановить переданные `common.Id` и `common.Type` и оставить `Params` пустым; общие изменяемые поля и параметры восстановит базовый сериализатор.

Диспетчер выбирает сериализатор по типу при записи и по сохранённому идентификатору при чтении. Сериализатор стандартного `Element` зарегистрирован по умолчанию; дополнительные реализации регистрируются явно:

```csharp
var dispatcher = new ElementSerializer();
dispatcher.Register(new DungeonSerializer());
byte[] data = dispatcher.Serialize(dungeon);
IElement restored = dispatcher.Deserialize(data);
```

На стороне чтения нужно зарегистрировать те же сериализаторы. Повторная регистрация типа или идентификатора и неизвестные типы отклоняются.

Значения параметров должны поддерживаться стандартным MessagePack resolver, а их типы — существующим `TypeNameHelper`; неподдерживаемые типы вызывают исключение. Имена типов восстанавливаются из данных, поэтому принимайте данные только из доверенных источников. Формат не является заменой существующему JSON-формату.

Параметры с `null` сохраняются, в том числе для ключей значимых типов. Преобразуемые значения сохраняются приведёнными к типу ключа. Конструктор `Element` с заданным GUID доступен наследникам для восстановления идентичности.
