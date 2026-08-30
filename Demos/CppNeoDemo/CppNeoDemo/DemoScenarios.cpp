#include "DemoScenarios.h"

DemoPoint MakeDemoPoint(double x, double y)
{
    DemoPoint demoPoint = { x, y };
    return demoPoint;
}

DemoLineSegment MakeDemoLineSegment(DemoPoint initial, DemoPoint final)
{
    DemoLineSegment demoLineSegment = { initial, final };
    return demoLineSegment;
}

DemoArcSegment MakeDemoArcSegment(DemoPoint center)
{
    DemoArcSegment demoArcSegment;
    demoArcSegment.demoCenterPoint = center;
    demoArcSegment.demoInitialAngle = 0;
    demoArcSegment.demoSweepAngle = 90;
    demoArcSegment.demoRadius = 10;
    return demoArcSegment;
}

DemoRectangle MakeDemoRectangle()
{
    DemoRectangle demoRectangle;
    demoRectangle.bottomLeftX = 0;
    demoRectangle.bottomLeftY = 0;
    demoRectangle.width = 30;
    demoRectangle.height = 10;
    return demoRectangle;
}

std::vector<DemoListOfItself> MakeDemoListOfItselfStorage()
{
    std::vector<DemoListOfItself> demoListOfItselfStorage(7);
    for (size_t i = 0; i < demoListOfItselfStorage.size(); i++)
    {
        demoListOfItselfStorage[i].x = i == 0 ? 0.0f : 10.0f;
        demoListOfItselfStorage[i].y = i == 0 ? 0.0f : 10.0f;
    }
    LinkCheckChain(demoListOfItselfStorage);
    return demoListOfItselfStorage;
}

DemoPointLinkedList MakeDemoListOfItself(std::vector<DemoListOfItself>& storage)
{
    DemoPointLinkedList demoListOfItself0(storage.empty() ? nullptr : storage.data(),
        (int)storage.size());
    return demoListOfItself0;
}

DemoSegment MakeDemoSegmentLine(DemoLineSegment line)
{
    DemoSegment demoSegmentLine;
    demoSegmentLine.type = DemoSegment::SegmentType::Line;
    demoSegmentLine.segment.line = line;
    return demoSegmentLine;
}

DemoSegment MakeDemoSegmentArc(DemoArcSegment arc)
{
    DemoSegment demoSegmentArc;
    demoSegmentArc.type = DemoSegment::SegmentType::Arc;
    demoSegmentArc.segment.arc = arc;
    return demoSegmentArc;
}

std::vector<DemoSegment> MakeDemoSegments(DemoSegment line, DemoSegment arc)
{
    std::vector<DemoSegment> demoSegments = { line, arc };
    return demoSegments;
}

std::vector<DemoSegment> MakeCheckMixedScenario()
{
    auto checkMixed = MakeCheckMixed();

    volatile int tick = 0;
    for (int step = 0; step < 20; step++)
    {
        tick = tick + 1; // F10 sin cambios en la variable.
    }

    checkMixed[6].segment.line.demoFinalPoint.demoX += 3.0;
    // Activa e inicializa la nueva alternativa de la union en el mismo paso.
    checkMixed[7] = MakeDemoSegmentLine(MakeDemoLineSegment({ 10.0, 10.0 }, { 17.0, 14.0 }));
    checkMixed.push_back(MakeDemoSegmentLine(MakeDemoLineSegment({ 2.0, 2.0 }, { 18.0, 18.0 })));

    return checkMixed;
}

std::vector<DemoListOfItself> MakeChainNodeStorage(int count)
{
    auto chainNodeStorage = MakeCheckChainNodes(count > 0 ? count : 0);
    LinkCheckChain(chainNodeStorage);
    // El retorno mueve el vector o elide la copia: las direcciones de los nodos se conservan.
    return chainNodeStorage;
}

DemoPointLinkedList MakeChainNodes(std::vector<DemoListOfItself>& chainNodeStorage)
{
    DemoPointLinkedList chainNodes(chainNodeStorage.empty() ? nullptr : chainNodeStorage.data(),
        (int)chainNodeStorage.size());

    volatile int tick = 0;
    for (int step = 0; step < 20; step++)
    {
        tick = tick + 1; // F10 sin cambios en la variable.
    }

    DemoListOfItself* changedPoint = chainNodes.GetAt(chainNodes.Count / 4);
    if (changedPoint != nullptr)
    {
        changedPoint->x += 3.0f;
        changedPoint->y += 3.0f;
    }

    return chainNodes;
}

DemoSegmentLinkedList MakeMixedChain(int count)
{
    DemoSegmentLinkedList mixedChain;
    FillCheckMixedChain(mixedChain, count);

    volatile int tick = 0;
    for (int step = 0; step < 20; step++)
    {
        tick = tick + 1; // Breakpoint: lista inicial; los pasos siguientes la modifican.
    }

    DemoSegmentChainNode* changedSegment = mixedChain.GetAt((mixedChain.Count / 2) & ~1);
    if (changedSegment != nullptr)
    {
        changedSegment->demoSegment.segment.line.demoFinalPoint.demoX += 3.0;
    }

    mixedChain.PushBack(MakeCheckMixedSegment(mixedChain.Count, mixedChain.Count + 1));
    mixedChain.RemoveAt(mixedChain.Count / 4);

    DemoSegmentChainNode* changedType = mixedChain.GetAt(mixedChain.Count / 2);
    if (changedType != nullptr)
    {
        changedType->demoSegment = MakeDemoSegmentLine(MakeDemoLineSegment({ 3.0, 3.0 }, { 12.0, 8.0 }));
    }

    return mixedChain;
}

std::vector<DemoPoint> MakeF10Points(int count)
{
    auto f10Points = MakeSpiralPoints(count > 0 ? count : 0);

    volatile int tick = 0;
    for (int step = 0; step < 50; step++)
    {
        tick = tick + 1; // F10 sin cambios en la variable.
    }

    if (!f10Points.empty())
    {
        const size_t changedIndex = f10Points.size() / 2;
        f10Points[changedIndex].demoX += 2.0;
        f10Points[changedIndex].demoY += 2.0;
    }
    f10Points.push_back(MakeDemoPoint(6.0, 6.0));

    return f10Points;
}
