#pragma once

#include "DemoCheckScenario.h"
#include "DemoStressTest.h"

DemoPoint MakeDemoPoint(double x, double y);
DemoLineSegment MakeDemoLineSegment(DemoPoint initial, DemoPoint final);
DemoArcSegment MakeDemoArcSegment(DemoPoint center);
DemoRectangle MakeDemoRectangle();
std::vector<DemoListOfItself> MakeDemoListOfItselfStorage();
DemoPointLinkedList MakeDemoListOfItself(std::vector<DemoListOfItself>& storage);
DemoSegment MakeDemoSegmentLine(DemoLineSegment line);
DemoSegment MakeDemoSegmentArc(DemoArcSegment arc);
std::vector<DemoSegment> MakeDemoSegments(DemoSegment line, DemoSegment arc);

std::vector<DemoSegment> MakeCheckMixedScenario();
std::vector<DemoListOfItself> MakeChainNodeStorage(int count);
DemoPointLinkedList MakeChainNodes(std::vector<DemoListOfItself>& chainNodeStorage);
DemoSegmentLinkedList MakeMixedChain(int count);
std::vector<DemoPoint> MakeF10Points(int count);
