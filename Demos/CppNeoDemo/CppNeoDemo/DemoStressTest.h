#pragma once

#include "DemoPoint.h"
#include "DemoLineSegment.h"
#include "demoArcSegment.h"
#include <vector>
#include <cmath>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

inline std::vector<DemoLineSegment> MakeGridSegments()
{
    std::vector<DemoLineSegment> result;

    for (int i = 0; i <= 44; i++)
    {
        DemoLineSegment h;
        h.demoInitialPoint = { (double)0,  (double)i };
        h.demoFinalPoint = { (double)44, (double)i };
        result.push_back(h);

        DemoLineSegment v;
        v.demoInitialPoint = { (double)i, (double)0  };
        v.demoFinalPoint = { (double)i, (double)44 };
        result.push_back(v);
    }

    for (int row = 0; row < 44; row++)
    {
        for (int col = 0; col < 44; col++)
        {
            DemoLineSegment d;
            d.demoInitialPoint = { (double)col, (double)row };
            d.demoFinalPoint = { (double)(col+1), (double)(row+1) };
            result.push_back(d);
        }
    }

    return result;
}

inline std::vector<DemoPoint> MakeSpiralPoints()
{
    std::vector<DemoPoint> result;
    result.reserve(2000);

    for (int i = 0; i < 2000; i++)
    {
        double t = i * (6.0 * M_PI / 2000.0);
        double r = 0.05 * t;
        double angle = t + (i % 2) * M_PI;
        DemoPoint p;
        p.demoX = 10.0 + r * std::cos(angle);
        p.demoY = 10.0 + r * std::sin(angle);
        result.push_back(p);
    }

    return result;
}

inline std::vector<DemoArcSegment> MakeConcentricArcs()
{
    std::vector<DemoArcSegment> result;

    const int rings = 20;
    const int arcsPerRing = 100;
    const double sweep = 360.0 / arcsPerRing * 0.8;

    for (int ring = 1; ring <= rings; ring++)
    {
        for (int a = 0; a < arcsPerRing; a++)
        {
            DemoArcSegment arc;
            arc.demoCenterPoint = { 10.0, 10.0 };
            arc.demoRadius = ring * 1.5;
            arc.demoInitialAngle = a * (360.0 / arcsPerRing);
            arc.demoSweepAngle = sweep;
            result.push_back(arc);
        }
    }

    return result;
}
