#pragma once
#include "DemoSegment.h"

class DemoSegmentChainNode
{
public:
    DemoSegmentChainNode();

    DemoSegmentChainNode* Next;
    DemoSegmentChainNode* Previous;

    DemoSegment demoSegment;
};

class DemoSegmentLinkedList
{
public:
    DemoSegmentLinkedList();
    ~DemoSegmentLinkedList();

    DemoSegmentLinkedList(const DemoSegmentLinkedList&) = delete;
    DemoSegmentLinkedList& operator=(const DemoSegmentLinkedList&) = delete;
    DemoSegmentLinkedList(DemoSegmentLinkedList&& other) noexcept;

    DemoSegmentChainNode* Head;
    DemoSegmentChainNode* Tail;
    int Count;

    DemoSegmentChainNode* PushBack(const DemoSegment& segment);
    DemoSegmentChainNode* GetAt(int index);
    bool RemoveAt(int index);
};
