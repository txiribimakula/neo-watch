#pragma once
#include "DemoLineSegment.h"
#include "DemoArcSegment.h"

class DemoSegment
{
public:
	enum SegmentType { Line, Arc };
	union Segment {
		DemoLineSegment line;
		DemoArcSegment arc;
	};
	SegmentType type;
	Segment segment;
};