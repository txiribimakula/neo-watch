#pragma once

#include "DemoPoint.h"
#include "DemoLineSegment.h"
#include "demoArcSegment.h"
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
