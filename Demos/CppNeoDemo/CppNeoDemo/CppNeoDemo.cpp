#include <iostream>
#include "DemoPoint.h"
#include "DemoLineSegment.h"
#include "demoArcSegment.h"
#include "DemoRectangle.h"

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

    std::cout << "Hello World!\n";
}
