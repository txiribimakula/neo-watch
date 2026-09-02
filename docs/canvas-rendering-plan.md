# Plan de optimizacion del canvas

Estado (2026-08-31): prototipo integrado, desactivado por defecto. La validacion
acelerada dentro de Visual Studio NO esta completada; no promover todavia.

## Implementado

- `NeoWatch.Drawer/Scene`: snapshots de valores, bloques inmutables de 2.048
  primitivas e indice jerarquico de limites. Las sustituciones copian solo los
  bloques afectados. Limites de dibujo y autofit separados para mantener el
  margen original de los puntos.
- `NeoWatch.Rendering` y `NeoWatch.Renderer.Native`: D3D11 con buffers persistentes,
  puntos/segmentos instanciados y arcos analiticos divididos en 8-512 regiones
  segun el zoom. Quads orientados para los segmentos, evitando pintar su bounding box.
  Los shaders se compilan en el build, no al abrir el canvas. El modulo nativo
  recibe bloques, no llamadas por figura.
- Camara independiente: pan/zoom/resize no transforman todos los drawables en
  modo GPU. Las peticiones se agrupan en el siguiente frame; no hay bucle en reposo.
- Seleccion por identidad, con indices cacheados. La GPU dibuja directamente la
  instancia seleccionada. Las versiones actual/anterior comparten bloques.
- Una mascara por capa conserva la opacidad de los solapamientos entre bloques.
  Se mantienen los huecos en el propio trazo grueso del fantasma seleccionado.
- Superficie D3D11/D3D9 compartida mediante `D3DImage`, sin readback a CPU en el
  camino de produccion. La UI, regla, coordenadas y overlays siguen siendo WPF.
- Preparacion inicial en un worker sobre datos de dominio nuevos, nunca sobre
  DTE/COM. Cancelacion por bloque y comprobacion de sesion/carga antes de publicar.
- Opcion `Tools > Options > Neo Watch > General > Enable GPU canvas (experimental)`,
  tambien disponible en opciones clasicas. Default `false`.
- Fallback completo al renderer anterior ante error de dispositivo, falta de
  soporte, WPF software, exceso de memoria o precision insuficiente. No se
  descartan figuras para aumentar velocidad. El motivo se registra en ActivityLog.

## Comprobaciones y medidas

- Solucion Debug/Release y tests MSTest. Banco reproducible en
  `Tests/CanvasHarness`; comandos y alcance en su `README.md`.
- Contrato de pixeles: seleccion de punto/linea/arco/circulo, rebobinado, sentido,
  degenerados y DPI 100/125/200 %. Se compara cobertura bidireccional con tolerancia
  de un DIP para antialiasing y colocacion de guiones en arcos WPF aproximados.
  La guarda numerica GPU es independiente: rechaza errores estimados > 0,2 pixeles.
- Pruebas adicionales de huecos/opacidad del fantasma, solapamientos entre bloques,
  borrado de la escena, buffers reutilizados y rechazo de coordenadas extremas.
- Repeticion de la base WPF: 100.000 figuras, conversiones Main 32,32 ms,
  Selected 5,68 ms, Caps 29,43 ms, Points 39,96 ms y SelectedPoint 6,05 ms.
  Son aproximadamente 113 ms de CPU, no tiempo de presentacion.
- Prototipo D3D11: 100.000 segmentos, mediana ~2,8-3,2 ms frente a ~66-69 ms con
  paths Direct2D cacheados. Esta comparacion NO incluye geometry realizations.
- 100.000 figuras mezcladas: mediana ~5,7 ms y p95 ~7,7 ms hasta completar la GPU.
  Un millon: mediana ~48 ms. No son FPS dentro de Visual Studio.
- Carga fria de 100.000 mixtas en una ejecucion: escena ~47 ms, dispositivo y
  shaders precompilados ~153 ms, primer frame GPU ~72 ms (incluye ~24 ms de subida).
  Se mide cada etapa por separado; no comparar un frame caliente con la carga fria.

## Validacion pendiente y limites

La instancia experimental de VS se ha arrancado y puesto en debug. Sin embargo,
su composicion WPF acelerada aparece en blanco en este entorno. Se reproduce en
una ventana WPF minima sin el renderer nuevo; en software si aparece. No se han
cambiado drivers ni ajustes graficos globales. Por tanto, no se da por validada la
presentacion acelerada real, el objetivo de 60 FPS ni la primera carga visible.

La prueba aislada `window gpu-software` muestra correctamente la superficie GPU
compartida junto a texto WPF mediante readback de diagnostico. Ese readback NO
esta activado en la extension y NO sirve como benchmark de presentacion GPU.

Antes de promover el modo:

- Resolver/repetir la prueba de composicion acelerada en VS y medir presentacion
  real (p95/p99), no solo finalizacion GPU. Probar docking, flotante, menus, DPI,
  varios monitores, minimizar, RDP y perdida de dispositivo.
- Comparar geometry realizations Direct2D si sigue siendo candidata. `HwndHost`
  no se ha implementado: su airspace obligaria a trasladar los overlays WPF.
- Ampliar capturas y pruebas interactivas de regla, seleccion, cancelacion y
  reinicio de debug sobre el backend acelerado.
- El indexado inicial y algunas comparaciones/listas de Items siguen siendo O(N).
  El indice de limites se reconstruye sobre bloques; no es una actualizacion
  logaritmica persistente de todos sus nodos.
- La sincronizacion con D3D9 espera a que termine la GPU, con limite de 250 ms.
  Una presentacion asincrona futura necesita pruebas de ownership y sincronizacion.
- La cache GPU admite 2.048 bloques residentes (aproximadamente 144 MiB de datos
  de instancias, mas metadatos y superficies). El exceso activa fallback; no omite
  contenido. Las superficies se limitan a 8.192 por lado y 16.777.216 pixeles.
- Los buffers usan coordenadas relativas por bloque y una guarda de error, no
  una promesa de representacion de cualquier rango con floats.

Las fases originales de abajo siguen siendo el criterio de aceptacion. Esta
implementacion deja una base experimental utilizable, no certifica todo el plan.

## Objetivo y alcance

Conseguir la mayor fluidez posible al visualizar geometria, manteniendo toda la
funcionalidad y la fiabilidad actuales. Se admite rehacer el motor del canvas;
la tabla, las opciones, los comandos y la integracion con el depurador seguirian
en WPF. No confundir el tiempo de lectura del depurador con el de visualizacion.

La propuesta es una escena persistente, independiente de WPF, con actualizaciones
incrementales y renderizado GPU. Elegir el backend mediante medidas en Visual
Studio, no por el nombre de la tecnologia.

## Diagnostico inicial

- `NeoWatch/ViewModel.cs`: pan, zoom y resize recorren y transforman las figuras.
- `NeoWatch/Converters/DrawablesToGeometryConverter.cs`: cada conversion recorre
  la coleccion, incluso para capas que solo representan una seleccion.
- El XAML ya agrupa geometria en `StreamGeometry`; no hay un control por figura.
  Hay cinco capas actuales y cuatro para el estado anterior.
- `NeoWatch.Drawer/DrawableCollection.cs`: reemplazos parciales pueden recalcular
  todos los limites y notificar un reset de la coleccion.
- Seleccion, comparacion con el estado anterior y reconstruccion de las capas
  provocan trabajo adicional sobre colecciones completas.
- El temporizador de carga actual no mide necesariamente el primer frame
  presentado. No basta para comparar motores.

Microbenchmark inicial, Release, puntos y segmentos mezclados, 20 repeticiones:

| Conversion | 10.000 | 50.000 | 100.000 |
| --- | ---: | ---: | ---: |
| Main | 3,99 ms | 16,82 ms | 33,69 ms |
| Selected | 0,15 ms | 2,80 ms | 5,68 ms |
| Caps | 3,75 ms | 13,78 ms | 33,15 ms |
| Points | 4,40 ms | 19,22 ms | 40,55 ms |
| SelectedPoint | 0,74 ms | 4,22 ms | 6,56 ms |

Las cinco conversiones suman aproximadamente 120 ms de CPU con 100.000 figuras,
sin rebobinado. No son FPS medidos dentro de Visual Studio. Otro ensayo con
`RenderTargetBitmap` incluye rasterizacion por software y tampoco representa
el rendimiento del canvas interactivo real. Repetir y documentar estas medidas
antes de usarlas como referencia de aceptacion.

## Arquitectura propuesta

`Datos del depurador -> escena independiente -> buffers persistentes -> GPU -> pantalla`

- Pan y zoom: cambiar la camara y consultar visibilidad, sin reconstruir ni
  transferir de nuevo toda la geometria.
- Seleccion: modificar el estado de los identificadores afectados.
- Mutacion: actualizar solo los bloques afectados.
- Rebobinado: reutilizar la version anterior ya disponible.
- Reposo: no solicitar frames innecesarios.

Esto elimina recorridos evitables de CPU, pero el trabajo de GPU sigue
dependiendo de las figuras visibles, los pixeles y el solapamiento.

## Fases

### 1. Medicion y contrato de comportamiento

- Preparar escenas de 10.000, 100.000 y 1.000.000 de puntos, segmentos, arcos y
  mezclas; incluir muchas figuras superpuestas y grandes coordenadas.
- Separar lectura, preparacion de escena, transferencia y presentacion. Medir
  primera carga en frio, primer frame visible y primer frame completo.
- Registrar p95/p99 de frames, CPU/GPU, memoria y GC durante pan, zoom, seleccion,
  cambios y rebobinado en la instancia real de Visual Studio.
- Fijar capturas y pruebas de referencia de todas las funciones actuales.

Salida: referencia reproducible y criterios de comparacion, sin cambiar motor.

### 2. Escena independiente

- Representacion compacta por bloques, inmutable por version, con identidades
  que mantengan la correspondencia entre Items, seleccion y canvas.
- Separar invalidaciones de datos, camara, estilo y seleccion.
- Preparar trabajo de CPU fuera del hilo UI tras obtener una copia segura de
  los datos; no trasladar llamadas COM/DTE arbitrariamente a otro hilo.
- Mantener un backend WPF como referencia y alternativa compatible.

Salida: contrato de escena comprobado sin alterar el resultado visual.

### 3. Prototipo GPU y decision de backend

- Probar D3D11 con primitivas agrupadas/instanciadas y buffers persistentes.
  Comparar con un prototipo pequeno de Direct2D y geometria cacheada; medir
  tambien el coste de mantener calidad al variar el zoom.
- Primera candidata para presentacion: HWND alojado con `HwndHost` y DXGI flip
  model, evitando copias de superficie hacia WPF.
- Validar antes de adoptarlo: airspace, recorte, foco, docking, menus y overlays.
  Regla, coordenadas e indicadores internos tendrian que dibujarse en el propio
  canvas; el texto puede usar DirectWrite/Direct2D.
- Si la integracion HWND no cumple, medir una superficie compartida mediante
  `D3DImage`, incluyendo el puente de interoperabilidad D3D9/D3D11, copias y
  sincronizacion. Evitar lecturas de vuelta a CPU.
- Si hay modulo nativo, intercambiar bloques, nunca una llamada por figura.
  Cambiar C# por C++ sin cambiar la arquitectura no resuelve este problema.
- No elegir DX12/Vulkan sin demostrar una ventaja que compense su complejidad.

Salida: decision medida dentro de VS. Completar fases 1-3 antes de invertir en
grandes optimizaciones especificas del renderer WPF actual.

### 4. Actualizaciones y renderizado incremental

- Subir a GPU solo bloques nuevos o modificados; compartir bloques sin cambios
  entre versiones actual/anterior y gestionar su vida util con seguridad.
- Indice espacial y limites conservadores que incluyan arcos, grosor y extremos.
  Actualizar limites incrementalmente y descartar solo geometria no visible.
- Agrupar entrada hasta el siguiente frame y usar la camara mas reciente.
- Arcos mediante evaluacion analitica o teselacion con error subpixel acotado.
  Evitar quads enormes con exceso de fragmentos transparentes.
- No eliminar ni agrupar figuras de forma que se pierda informacion para aparentar
  rendimiento. Mantener precision, con coordenadas relativas a camara cuando
  sea necesario para los buffers float de GPU.
- Publicar cada version de tabla/canvas de forma coherente; ignorar resultados
  de cargas canceladas o de una sesion de depuracion terminada.

### 5. Paridad funcional y visual

Conservar puntos, segmentos, arcos/circulos, colores, opacidad, orden de dibujo,
grosor aparente, tamano de puntos, sentido/extremos, autofit, regla, coordenadas,
visibilidad, seleccion, rebobinado, resaltado de Items, cancelacion y foco/menus.

- Activar rebobinado no debe cambiar la seleccion actual.
- La seleccion de la figura anterior se hace dejando huecos en su propio trazo
  grueso y transparente, no superponiendo una linea fina ni borrando el fondo.
- Respetar la composicion entre capas: dibujar cada primitiva por separado puede
  oscurecer solapamientos frente al `Path` agrupado actual.
- No reordenar lotes si cambia el orden visual.
- No deducir identidad estable de objetos solo por su direccion en memoria.

### 6. Validacion y despliegue

- Pruebas geometricas y capturas de referencia con tolerancia explicita para
  antialiasing y error subpixel; incluir arcos negativos, circulos completos,
  degenerados, coordenadas extremas y elementos seleccionados/modificados/borrados.
- Probar en VS: panel acoplado/flotante, resize, DPI, varios monitores, escritorio
  remoto, minimizar, perdida del dispositivo GPU y reinicio de depuracion.
- Limitar caches y liberar recursos al cerrar; verificar memoria durante cargas
  y rebobinados repetidos. Mantener una alternativa WPF/software funcional.
- Publicar inicialmente como opcion experimental desactivada. Promover solo
  cuando cumpla paridad y mejore el rendimiento sin penalizar escenas pequenas.

## Objetivos de aceptacion propuestos

- En el equipo del usuario y escenas acordadas de 100.000 figuras: 60 FPS
  sostenidos y p95 de frame <= 16,7 ms; 120 FPS es un objetivo aspiracional.
- Seleccion visible en el siguiente frame disponible, sin reconstruccion completa.
- Pan/zoom sin trabajo CPU proporcional a toda la geometria para transformarla.
- Primera carga medida hasta el frame completo, sin esconder trabajo pendiente.
- Un millon de figuras como prueba de limites, no como promesa de rendimiento.
- Ninguna regresion funcional, de precision ni de aislamiento entre sesiones.

No hay un factor de mejora garantizado ni una tecnologia demostrada como la mas
rapida hasta medir los prototipos. La prioridad es fiabilidad y paridad.

## Documentacion tecnica de referencia

- [Direct2D geometry realizations](https://learn.microsoft.com/en-us/windows/win32/direct2d/geometry-realizations-overview)
- [WPF y Win32: interoperabilidad y limitaciones](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-and-win32-interoperation)
- [Rendimiento de Direct3D9 y WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/performance-considerations-for-direct3d9-and-wpf-interoperability)
- [D3DImage e interoperabilidad](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-and-direct3d9-interoperation)
- [DXGI flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model)
- [D3D11 DrawInstanced](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-drawinstanced)
- [Recursos dinamicos D3D11](https://learn.microsoft.com/en-us/windows/win32/direct3d11/how-to--use-dynamic-resources)
- [Rendimiento de graficos 2D en WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-2d-graphics-and-imaging)

## Como retomar

Retomar las comprobaciones de "Validacion pendiente y limites". No activar el
renderer por defecto hasta validar presentacion acelerada y paridad dentro de VS.
