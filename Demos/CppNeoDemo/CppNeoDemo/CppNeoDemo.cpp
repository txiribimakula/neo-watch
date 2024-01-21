#include <iostream>
#include "DemoPoint.h"
#include "DemoLineSegment.h"
#include "DemoArcSegment.h"
#include "DemoRectangle.h"
#include "DemoListOfItself.h"
#include "DemoSegment.h"
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
}
