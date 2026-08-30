#include "DemoScenarios.h"

int main()
{
    auto demoPoint0 = MakeDemoPoint(0.0, 0.0);
    auto demoPoint1 = MakeDemoPoint(10.0, 10.0);
    auto demoLineSegment = MakeDemoLineSegment(demoPoint0, demoPoint1);
    auto demoArcSegment = MakeDemoArcSegment(demoPoint0);
    auto demoRectangle = MakeDemoRectangle();

    auto demoListOfItselfStorage = MakeDemoListOfItselfStorage();
    auto demoListOfItself0 = MakeDemoListOfItself(demoListOfItselfStorage);

    auto demoSegmentLine = MakeDemoSegmentLine(demoLineSegment);
    auto demoSegmentArc = MakeDemoSegmentArc(demoArcSegment);
    auto demoSegments = MakeDemoSegments(demoSegmentLine, demoSegmentArc);

    auto stressSegments = MakeGridSegments();
    auto stressPoints = MakeSpiralPoints();
    auto stressArcs = MakeConcentricArcs();

    auto checkPoints = MakeCheckPoints();
    auto checkSegments = MakeCheckSegments();
    auto checkArcs = MakeCheckArcs();
    auto checkNested = MakeCheckNested();
    auto checkMixed = MakeCheckMixedScenario();

    auto chainNodeStorage = MakeChainNodeStorage(2000);
    auto chainNodes = MakeChainNodes(chainNodeStorage);
    auto mixedChain = MakeMixedChain(1800);
    auto f10Points = MakeF10Points(30000);

    return 0; // Breakpoint: todas las variables estan listas para cargar.
}
