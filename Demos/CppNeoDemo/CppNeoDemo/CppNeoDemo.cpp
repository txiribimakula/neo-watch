#include <iostream>
#include "DemoPoint.h"
#include "DemoLineSegment.h"
#include "DemoArcSegment.h"
#include "DemoRectangle.h"
#include "DemoListOfItself.h"
#include "DemoSegment.h"
#include "DemoSegmentChainNode.h"
#include "DemoStressTest.h"
#include "DemoCheckScenario.h"
#include <vector>

int main()
{
    DemoPoint demoPoint0;
    demoPoint0.demoX = 0;
    demoPoint0.demoY = 0;

    DemoPoint demoPoint1;
    demoPoint1.demoX = 10;
    demoPoint1.demoY = 10;

    DemoLineSegment demoLineSegment;
    demoLineSegment.demoInitialPoint = demoPoint0;
    demoLineSegment.demoFinalPoint = demoPoint1;

    DemoArcSegment demoArcSegment;
    demoArcSegment.demoCenterPoint = demoPoint0;
    demoArcSegment.demoInitialAngle = 0;
    demoArcSegment.demoSweepAngle = 90;
    demoArcSegment.demoRadius = 10;

    DemoRectangle demoRectangle;
    demoRectangle.bottomLeftX = 0;
    demoRectangle.bottomLeftY = 0;
    demoRectangle.width = 30;
    demoRectangle.height = 10;

    DemoListOfItself demoListOfItself0;
    demoListOfItself0.Previous = nullptr;
    demoListOfItself0.x = 0;
    demoListOfItself0.y = 0;
    DemoListOfItself demoListOfItself1;
    demoListOfItself1.x = 10;
    demoListOfItself1.y = 10;
    demoListOfItself0.Next = &demoListOfItself1;
    DemoListOfItself demoListOfItself2;
    demoListOfItself2.x = 10;
    demoListOfItself2.y = 10;
    demoListOfItself1.Next = &demoListOfItself2;
    DemoListOfItself demoListOfItself3;
    demoListOfItself3.x = 10;
    demoListOfItself3.y = 10;
    demoListOfItself2.Next = &demoListOfItself3;
    DemoListOfItself demoListOfItself4;
    demoListOfItself4.x = 10;
    demoListOfItself4.y = 10;
    demoListOfItself3.Next = &demoListOfItself4;
    DemoListOfItself demoListOfItself5;
    demoListOfItself5.x = 10;
    demoListOfItself5.y = 10;
    demoListOfItself4.Next = &demoListOfItself5;
    DemoListOfItself demoListOfItself6;
    demoListOfItself6.x = 10;
    demoListOfItself6.y = 10;
    demoListOfItself5.Next = &demoListOfItself6;

    DemoSegment demoSegmentLine;
    demoSegmentLine.type = DemoSegment::SegmentType::Line;
    demoSegmentLine.segment.line = demoLineSegment;
    DemoSegment demoSegmentArc;
    demoSegmentArc.type = DemoSegment::SegmentType::Arc;
    demoSegmentArc.segment.arc = demoArcSegment;
    std::vector<DemoSegment> demoSegments;
    demoSegments.push_back(demoSegmentLine);
    demoSegments.push_back(demoSegmentArc);

    std::cout << "Hello World!\n";

    auto stressSegments = MakeGridSegments();
    auto stressPoints   = MakeSpiralPoints();
    auto stressArcs     = MakeConcentricArcs();

    // =====================================================================
    // ESCENARIO RAPIDO - validar A1-A3 (ver docs/optimizacion-f10.html)
    //
    // Carga instantanea. Pon el breakpoint en el bucle de aqui abajo, anade los
    // tres vectores al Neo Watch y no hace falta llegar nunca al bloque pesado.
    // =====================================================================
    auto checkPoints   = MakeCheckPoints();     // 40 puntos en anillo
    auto checkSegments = MakeCheckSegments();   // 20 segmentos en abanico
    auto checkArcs     = MakeCheckArcs();       // 12 arcos en espiral
    auto checkNested   = MakeCheckNested();     // 3 rectangulos -> 12 segmentos, lista anidada

    volatile int checkTick = 0;
    for (int step = 0; step < 20; step++)
    {
        checkTick = checkTick + 1;              // <-- breakpoint aqui para el escenario rapido
    }

    // =====================================================================
    // UNION DISCRIMINADA Y LISTA ENLAZADA - los dos extremos de la deteccion
    // por memoria (ver docs/optimizacion-f10.html, filas C0, C0b y C0c).
    //
    // 'checkMixed' es contiguo y POD: entra en la deteccion por memoria, y
    // ademas cada elemento da un drawable, asi que entra en la recarga puntual.
    //
    // 'chainNodes[0]' es una lista enlazada de puntos: no hay bloque contiguo
    // que leer, asi que NO entra en nada de eso y se recarga entera en cada paso.
    //
    // 'mixedChain' combina los dos casos: una lista doblemente enlazada propia
    // cuyos nodos contienen la misma union discriminada de linea/arco. No hay
    // array de nodos; el aspecto de lista lo da NatVis.
    // =====================================================================
    auto checkMixed = MakeCheckMixed();              // 24 elementos: lineas y arcos alternados
    auto chainNodes = MakeCheckChainNodes(200);      // 200 nodos de puntos...
    DemoSegmentLinkedList mixedChain;                // 18 nodos propios de lineas/arcos
    FillCheckMixedChain(mixedChain, 18);
    LinkCheckChain(chainNodes);                      // ...enlazados ya en su sitio definitivo

    volatile int mixedTick = 0;
    for (int step = 0; step < 20; step++)
    {
        mixedTick = mixedTick + 1;              // <-- breakpoint aqui. Anade 'checkMixed',
    }                                           //     'chainNodes[0]' y 'mixedChain'

    // Mueve el extremo de una linea: cambian bytes dentro de la union.
    checkMixed[6].segment.line.demoFinalPoint.demoX += 3.0;   // <-- F10: recarga puntual

    // Cambia el discriminante: los MISMOS bytes de la union pasan a leerse como
    // linea en vez de arco. Se toca poco mas que un byte y el dibujo cambia entero.
    checkMixed[7].type = DemoSegment::SegmentType::Line;      // <-- F10: recarga puntual

    // Crece por el final.
    DemoSegment extraSegment;
    extraSegment.type = DemoSegment::SegmentType::Line;
    extraSegment.segment.line.demoInitialPoint = { 2.0, 2.0 };
    extraSegment.segment.line.demoFinalPoint = { 18.0, 18.0 };
    checkMixed.push_back(extraSegment);                       // <-- F10: solo el anadido

    // La lista enlazada no tiene atajo posible: cada uno de estos pasos recorre
    // los 200 nodos por COM. Debe seguir dibujando bien, solo que sin acelerar.
    chainNodes[50].x += 3.0f;                                 // <-- F10: recarga completa
    chainNodes[50].y += 3.0f;                                 // <-- F10: recarga completa

    // Cambia un valor dentro de un nodo existente. El snapshot detecta que la
    // lista ha cambiado y Neo Watch vuelve a dibujarla.
    mixedChain.GetAt(6)->demoSegment.segment.line.demoFinalPoint.demoX += 3.0; // <-- F10: cambia un valor

    // Anade un arco al final: cambian Count, Tail y los enlaces entre nodos.
    mixedChain.PushBack(MakeCheckMixedSegment(19, 20));                      // <-- F10: aparece un elemento

    // Quita un nodo intermedio: se actualizan Next y Previous a ambos lados.
    mixedChain.RemoveAt(4);                                                  // <-- F10: desaparece un elemento

    // Tras el borrado, el indice 6 contiene el arco que originalmente estaba
    // en el indice 7. Cambia el tipo activo de la union de arco a linea.
    mixedChain.GetAt(6)->demoSegment.type = DemoSegment::SegmentType::Line;  // <-- F10: cambia la geometria

    // =====================================================================
    // ESCENARIO PESADO - solo para la prueba de tiron con la recarga desactivada.
    //
    // Tarda en cargar a proposito. No hace falta para validar A1-A3: si solo vas
    // a eso, quedate arriba. Baja el tamano si te estorba; el coste escala lineal.
    // =====================================================================
    auto f10Points = MakeSpiralPoints(3000);

    volatile int tick = 0;
    for (int step = 0; step < 50; step++)
    {
        tick = tick + 1;                        // <-- breakpoint aqui para la prueba de tiron
    }

    // =====================================================================
    // COMPROBAR QUE UN CAMBIO SI RECARGA (C0)
    //
    // Con 'f10Points' en el Neo Watch, sigue dando F10. El bucle de arriba no
    // toca el vector, asi que esos pasos deben ser inmediatos. Estos tres si lo
    // tocan, y cada uno tiene que provocar una recarga completa: barra de
    // progreso y el dibujo cambiando.
    // =====================================================================

    // 1. Modificar en el sitio. Misma direccion base y mismo tamano: el
    //    DisplayString del vector sigue diciendo "{ size=3000 }". Lo unico que
    //    delata el cambio es comparar los bytes.
    f10Points[1500].demoX += 2.0;               // <-- F10: debe recargar
    f10Points[1500].demoY += 2.0;               // <-- F10: debe recargar

    // 2. Anadir uno. Cambia el tamano a "{ size=3001 }" y, si el vector realoja,
    //    tambien la direccion base. Se detecta antes de leer un solo byte, y el
    //    desplegable Items pasa a tener un elemento mas.
    DemoPoint extraPoint;
    extraPoint.demoX = 6.0;
    extraPoint.demoY = 6.0;
    f10Points.push_back(extraPoint);            // <-- F10: debe recargar

    std::cout << "ticks: " << (checkTick + mixedTick + tick)
              << " puntos: " << f10Points.size() << std::endl;
}
