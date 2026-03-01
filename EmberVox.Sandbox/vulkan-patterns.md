# C++ → C# Vulkan Translation Patterns

## 1. Enumeração de propriedades

```cpp
auto things = context.enumerateThings();
```

```csharp
uint count = 0;
_vk.EnumerateThings(..., ref count, null);

Thing[] things = new Thing[count];
fixed (Thing* pThings = things)
    _vk.EnumerateThings(..., ref count, pThings);
```

---

## 2. Strings em structs

```cpp
vk::SomeStruct{ .pName = "valor" }
```

```csharp
new SomeStruct { PName = (byte*)SilkMarshal.StringToPtr("valor") }
// Sempre liberar depois:
SilkMarshal.Free((nint)struct.PName);
```

Array de strings:
```cpp
std::vector<const char*> names = { "a", "b" };
createInfo.ppNames = names.data();
```

```csharp
string[] names = ["a", "b"];
createInfo.PpNames = (byte**)SilkMarshal.StringArrayToPtr(names);
// Sempre liberar depois:
SilkMarshal.Free((nint)createInfo.PpNames);
```

---

## 3. Verificação de suporte (any_of / find_if)

```cpp
std::ranges::any_of(things, [](auto const& t) { return t.flag & SomeBit; });
```

```csharp
things.Any(t => (t.Flag & SomeBit) != 0);
```

```cpp
std::ranges::all_of(required, [&things](auto const& r) {
    return std::ranges::any_of(things, [r](auto const& t) {
        return strcmp(t.name, r) == 0;
    });
});
```

```csharp
required.All(r => things.Any(t =>
    SilkMarshal.PtrToString((nint)t.Name) == r
));
```

---

## 4. Structs com ponteiro pra outra struct

```cpp
vk::SomeCreateInfo createInfo{ .pNext = &otherStruct }
```

```csharp
SomeCreateInfo createInfo = new()
{
    SType = StructureType.SomeCreateInfo, // obrigatório em C#!
    PNext = &otherStruct,
};
```

> Em C++ o `sType` é preenchido automaticamente pela RAII. Em C# você sempre precisa setar manualmente.

---

## 5. Error handling

```cpp
throw std::runtime_error("msg");
```

```csharp
throw new Exception("msg");
```

```cpp
if (result != vk::Result::eSuccess)
    throw std::runtime_error("msg");
```

```csharp
if (result != Result.Success)
    throw new Exception("msg");
```

---

## 6. Containers

| C++ | C# |
|-----|----|
| `std::vector<T>` | `List<T>` ou `T[]` |
| `std::array<T, N>` | `T[N]` |
| `std::string` | `string` |
| `const char*` | `byte*` |
| `const char**` | `byte**` |
