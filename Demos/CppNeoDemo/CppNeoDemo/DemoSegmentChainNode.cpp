#include "DemoSegmentChainNode.h"

DemoSegmentChainNode::DemoSegmentChainNode()
    : Next(nullptr),
      Previous(nullptr)
{
}

DemoSegmentLinkedList::DemoSegmentLinkedList()
    : Head(nullptr),
      Tail(nullptr),
      Count(0)
{
}

DemoSegmentLinkedList::DemoSegmentLinkedList(DemoSegmentLinkedList&& other) noexcept
    : Head(other.Head),
      Tail(other.Tail),
      Count(other.Count)
{
    other.Head = nullptr;
    other.Tail = nullptr;
    other.Count = 0;
}

DemoSegmentLinkedList::~DemoSegmentLinkedList()
{
    DemoSegmentChainNode* current = Head;
    while (current != nullptr)
    {
        DemoSegmentChainNode* next = current->Next;
        delete current;
        current = next;
    }
}

DemoSegmentChainNode* DemoSegmentLinkedList::PushBack(const DemoSegment& segment)
{
    DemoSegmentChainNode* node = new DemoSegmentChainNode();
    node->demoSegment = segment;
    node->Previous = Tail;

    if (Tail != nullptr)
    {
        Tail->Next = node;
    }
    else
    {
        Head = node;
    }

    Tail = node;
    Count++;
    return node;
}

DemoSegmentChainNode* DemoSegmentLinkedList::GetAt(int index)
{
    if (index < 0 || index >= Count)
    {
        return nullptr;
    }

    DemoSegmentChainNode* current;
    if (index < Count / 2)
    {
        current = Head;
        for (int i = 0; i < index; i++)
        {
            current = current->Next;
        }
    }
    else
    {
        current = Tail;
        for (int i = Count - 1; i > index; i--)
        {
            current = current->Previous;
        }
    }

    return current;
}

bool DemoSegmentLinkedList::RemoveAt(int index)
{
    DemoSegmentChainNode* node = GetAt(index);
    if (node == nullptr)
    {
        return false;
    }

    if (node->Previous != nullptr)
    {
        node->Previous->Next = node->Next;
    }
    else
    {
        Head = node->Next;
    }

    if (node->Next != nullptr)
    {
        node->Next->Previous = node->Previous;
    }
    else
    {
        Tail = node->Previous;
    }

    delete node;
    Count--;
    return true;
}
