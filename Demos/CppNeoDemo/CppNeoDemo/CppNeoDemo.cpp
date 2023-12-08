#include <iostream>
#include "DemoPoint.h"
#include "DemoLineSegment.h"
#include "DemoArcSegment.h"
#include "DemoRectangle.h"
#include "DemoListOfItself.h"

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

    std::cout << "Hello World!\n";
}
