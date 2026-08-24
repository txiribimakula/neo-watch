#pragma once

#include "DemoPoint.h"
#include "DemoLineSegment.h"
#include "demoArcSegment.h"
#include "DemoRectangle.h"
#include "DemoSegment.h"
#include "DemoListOfItself.h"
#include <vector>
#include <cmath>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

// Escenario pequeno para validar a ojo A1-A3.
//
// Carga en un instante y entre los tres vectores ejercita los cinco modos de dibujado
// del converter: Main, Selected, Caps, Points y SelectedPoint. Las figuras estan
// separadas a proposito para que se distinga cual es la seleccionada.
//
// Ver docs/optimizacion-f10.html, fila A1.

// 40 puntos en anillo: separados de sobra para ver cual esta resaltado.
inline std::vector<DemoPoint> MakeCheckPoints()
{
    std::vector<DemoPoint> result;

    const int count = 40;
    for (int i = 0; i < count; i++)
    {
        const double angle = i * (2.0 * M_PI / count);
        DemoPoint p;
        p.demoX = 10.0 + 8.0 * std::cos(angle);
        p.demoY = 10.0 + 8.0 * std::sin(angle);
        result.push_back(p);
    }

    return result;
}

// 20 segmentos en abanico desde el centro: el seleccionado se dibuja discontinuo,
// y con Toggle Sense se ve la punta de direccion de cada uno.
inline std::vector<DemoLineSegment> MakeCheckSegments()
{
    std::vector<DemoLineSegment> result;

    const int count = 20;
    for (int i = 0; i < count; i++)
    {
        const double angle = i * (2.0 * M_PI / count);
        DemoLineSegment segment;
        segment.demoInitialPoint = { 10.0, 10.0 };
        segment.demoFinalPoint = { 10.0 + 6.0 * std::cos(angle), 10.0 + 6.0 * std::sin(angle) };
        result.push_back(segment);
    }

    return result;
}

// 12 arcos en espiral: radios distintos para poder seguir cual cambia al seleccionar.
inline std::vector<DemoArcSegment> MakeCheckArcs()
{
    std::vector<DemoArcSegment> result;

    const int count = 12;
    for (int i = 0; i < count; i++)
    {
        DemoArcSegment arc;
        arc.demoCenterPoint = { 10.0, 10.0 };
        arc.demoRadius = 2.0 + i * 0.35;
        arc.demoInitialAngle = i * 30.0;
        arc.demoSweepAngle = 200.0;
        result.push_back(arc);
    }

    return result;
}

// Contenedor de contenedores: cada DemoRectangle tiene DisplayString "List" y su NatVis
// lo expande en 4 segmentos sinteticos. Es el unico caso del demo donde ExpressionLoader
// tiene que detectar una lista ANIDADA, leyendo Type y Value de cada elemento.
// Sin esto, B2 podria romper la deteccion sin que ninguna prueba lo notase.
inline std::vector<DemoRectangle> MakeCheckNested()
{
    std::vector<DemoRectangle> result;

    for (int i = 0; i < 3; i++)
    {
        DemoRectangle rectangle;
        rectangle.bottomLeftX = 2.0f + i * 1.5f;
        rectangle.bottomLeftY = 2.0f + i * 1.5f;
        rectangle.width = 12.0f - i * 3.0f;
        rectangle.height = 8.0f - i * 2.0f;
        result.push_back(rectangle);
    }

    return result;
}

// Contenedor heterogeneo: una union discriminada donde cada elemento es linea o arco
// segun su campo 'type'. Contiguo y POD, asi que la deteccion por memoria SI aplica,
// y cada elemento produce exactamente un drawable, asi que la recarga puntual tambien.
//
// Lo interesante es que cambiar 'type' reinterpreta los mismos bytes de la union: el
// dibujo cambia por completo aunque se toque poco mas que un byte.
inline std::vector<DemoSegment> MakeCheckMixed()
{
    std::vector<DemoSegment> result;

    const int count = 24;
    for (int i = 0; i < count; i++)
    {
        const double angle = i * (2.0 * M_PI / count);
        DemoSegment item;

        if (i % 2 == 0)
        {
            item.type = DemoSegment::SegmentType::Line;
            item.segment.line.demoInitialPoint = { 10.0, 10.0 };
            item.segment.line.demoFinalPoint = { 10.0 + 7.0 * std::cos(angle), 10.0 + 7.0 * std::sin(angle) };
        }
        else
        {
            item.type = DemoSegment::SegmentType::Arc;
            item.segment.arc.demoCenterPoint = { 10.0, 10.0 };
            item.segment.arc.demoRadius = 3.0 + (i % 6) * 0.6;
            item.segment.arc.demoInitialAngle = i * 15.0;
            item.segment.arc.demoSweepAngle = 120.0;
        }

        result.push_back(item);
    }

    return result;
}

// Nodos de una lista enlazada, SIN enlazar todavia: enlazarlos aqui no serviria de nada
// porque devolver el vector por valor moveria el buffer y dejaria los punteros colgando.
inline std::vector<DemoListOfItself> MakeCheckChainNodes(int count)
{
    std::vector<DemoListOfItself> result(count);

    for (int i = 0; i < count; i++)
    {
        const double angle = i * (2.0 * M_PI / count);
        result[i].x = (float)(10.0 + 7.5 * std::cos(angle));
        result[i].y = (float)(10.0 + 7.5 * std::sin(angle));
        result[i].Next = nullptr;
        result[i].Previous = nullptr;
    }

    return result;
}

// Enlaza los nodos una vez el vector ya esta en su sitio definitivo.
inline void LinkCheckChain(std::vector<DemoListOfItself>& nodes)
{
    for (size_t i = 0; i < nodes.size(); i++)
    {
        nodes[i].Previous = (i == 0) ? nullptr : &nodes[i - 1];
        nodes[i].Next = (i + 1 == nodes.size()) ? nullptr : &nodes[i + 1];
    }
}
