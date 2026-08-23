# NeoWatch.Benchmark

Banco de pruebas para medir el coste de render que dispara **un F10** en la ventana de Neo Watch.

Replica la secuencia de notificaciones de `ViewModel.OnWatchItemReloadAsync` contra los **5 `MultiBinding` reales** declarados en [`NeoWatchWindow.xaml:96-137`](../NeoWatch/Views/NeoWatchWindow.xaml), y cuenta cuántas veces se ejecuta de verdad cada converter de geometría.

No hay mocks: `DrawablesToGeometryConverter` y `WatchItem` se compilan desde los fuentes de producción vía `Compile`/`Link` en el `.csproj`, así que el banco no puede desincronizarse del código real.

## Uso

```bash
msbuild NeoWatch.Benchmark/NeoWatch.Benchmark.csproj /t:Restore;Build /p:Configuration=Release
```

```bash
NeoWatch.Benchmark/bin/Release/net472/NeoWatch.Benchmark.exe 5000 30
```

Argumentos: `[numDrawables] [numPasos]` (por defecto `5000 20`).

El proyecto es autónomo — **no está en `NeoWatch.sln`** y CI no lo compila.

## Variantes que mide

| Variante | Cambio |
|---|---|
| `Actual` | Lo que hay hoy: [`ViewModel.cs:488`](../NeoWatch/ViewModel.cs) fuera del `if (IsLoading)`, y [`WatchItem.cs:92`](../NeoWatch.Loader/WatchItem.cs) llamando siempre a `NotifyGeometriesChanged` |
| `V1` | Mover ese bloque dentro del `if (IsLoading)` + guarda de igualdad |
| `V2` | V1 + el setter de `SelectedItem` deja de llamar a `NotifyGeometriesChanged` (la asignación pasa antes del notify de colección) |
| `V3` | V2 + los 5 `MultiBinding` dejan de enlazar `SelectedItem`; el converter lo lee de la colección, dejando `GeometryVersion` como única fuente |

Las variantes V2 y V3 se simulan por reflexión sobre los miembros privados de `WatchItem` (ver `SetSelectionWithoutGeometryNotify`), para poder medirlas **sin tocar todavía el código de producción**.

## Cómo leer los resultados

La columna **Pasadas por F10** es exacta y determinista: es la métrica que importa. Los **ms** tienen ruido de ±15% entre ejecuciones (se toma el mejor de 3), y escalan con `numDrawables`.
