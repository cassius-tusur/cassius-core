# BaseClasses

`Plot` owns a collection of story elements and their typed relations. Element
identity is its immutable `Id`: content changes do not change identity, and a
plot rejects a distinct instance whose ID collides with an existing element.

## Standard relation routes

`Plot.Bind` materializes the reciprocal relation only for the source and target
types shown below. Standard keys reject unregistered type pairs. A registered
custom strategy takes precedence; unregistered custom keys remain one-sided. An explicitly unregistered route
also falls back to a one-sided relation. An existing relation with an unsupported
standard route can be removed one-sided; new bindings for that route are rejected.

| Forward key | Reciprocal key | Supported source → target | Direction |
| --- | --- | --- | --- |
| `Parent` | `Child` | `Character` → `Character` | Parent to child; multiple parents are permitted. |
| `Child` | `Parent` | `Character` → `Character` | Child to parent. |
| `Contains` | `ContainedBy` | `Location` → `Location` | Container to contained location. |
| `ContainedBy` | `Contains` | `Location` → `Location` | Contained location to container. |
| `Passage` | `Passage` | `Location` ↔ `Location` | Symmetric location-to-location passage. |

All five keys are `ParamKey<bool>` values in `BaseRelationKeys`.
# MessagePack для элемента

`ElementMessagePackSerializer` сохраняет общие поля отдельного `IElement`: GUID, тип, название, описание, время и типизированные параметры. Для всего сюжета используйте `PlotMessagePackSerializer`.

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

## MessagePack для сюжета

```csharp
var elements = new ElementSerializer();
// elements.Register(new DungeonSerializer()); // для собственной реализации IElement
var serializer = new PlotMessagePackSerializer(elements);
byte[] bytes = serializer.Serialize(plot);
Plot restored = serializer.Deserialize(bytes);
```

Сохраняются время сюжета, элементы и связи вместе со значениями и типами ключей. Связи восстанавливаются между теми же экземплярами элементов, которые находятся в восстановленном сюжете. Зарегистрируйте одни и те же сериализаторы реализаций `IElement` при записи и чтении. Стратегии `Binder` не сохраняются; для дальнейшей работы с пользовательским binder передайте его вторым аргументом в `Deserialize(bytes, binder)`.

Для значений `object` сериализатор записывает тип каждого вложенного значения. Это позволяет сохранить, например, `Guid`, `decimal` и вложенные словари.

Параметры с `null` сохраняются, в том числе для ключей значимых типов. Преобразуемые значения сохраняются приведёнными к типу ключа. Конструктор `Element` с заданным GUID доступен наследникам для восстановления идентичности.

JSON-сериализатор также сохраняет типы значений внутри `object`. При чтении
обычных JSON-значений без метаданных тип определяется по содержимому.
В пользовательских значениях параметров и связей имена JSON-свойств `$type` и
`$value` зарезервированы для метаданных типа. При сериализации такие имена
отклоняются, в том числе во вложенных объектах и словарях.
