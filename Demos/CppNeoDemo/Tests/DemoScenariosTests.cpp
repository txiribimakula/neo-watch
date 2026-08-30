#include "../CppNeoDemo/DemoScenarios.h"
#include <cassert>
#include <iostream>
#include <utility>

static void CheckLinks(DemoSegmentLinkedList& list, int count)
{
    assert(list.Count == count);
    auto node = list.Head;
    DemoSegmentChainNode* previous = nullptr;
    for (int i = 0; i < count; i++)
    {
        assert(node != nullptr && node->Previous == previous);
        assert(list.GetAt(i) == node);
        assert(node->demoSegment.type == DemoSegment::Line || node->demoSegment.type == DemoSegment::Arc);
        previous = node;
        node = node->Next;
    }
    assert(node == nullptr && list.Tail == previous);
    node = list.Tail;
    for (int i = count - 1; i >= 0; i--)
    {
        assert(node == list.GetAt(i));
        node = node->Previous;
    }
    assert(node == nullptr);
}

int main()
{
    auto basicStorage = MakeDemoListOfItselfStorage();
    auto basicList = MakeDemoListOfItself(basicStorage);
    assert(basicList.Count == 7 && basicList.Head == basicStorage.data());
    assert(basicList.GetAt(6)->Next == nullptr && basicList.Head->Previous == nullptr);
    assert(basicList.Head->x == 0 && basicList.GetAt(6)->x == 10);

    auto mixed = MakeCheckMixedScenario();
    assert(mixed.size() == 25 && mixed[7].type == DemoSegment::Line);
    assert(mixed.back().segment.line.demoFinalPoint.demoX == 18);

    for (int count : { -1, 0, 1, 2, 7, 18, 2000 })
    {
        const int expected = count > 0 ? count : 0;
        auto storage = MakeChainNodeStorage(count);
        auto address = storage.data();
        auto movedStorage = std::move(storage);
        assert(movedStorage.data() == address);
        assert(movedStorage.size() == (size_t)expected);
        for (int i = 0; i < expected; i++)
        {
            assert(movedStorage[i].Previous == (i == 0 ? nullptr : &movedStorage[i - 1]));
            assert(movedStorage[i].Next == (i + 1 == expected ? nullptr : &movedStorage[i + 1]));
        }
        float oldX = expected == 0 ? 0 : movedStorage[expected / 4].x;
        auto points = MakeChainNodes(movedStorage);
        assert(points.Count == expected);
        assert(points.Head == (expected == 0 ? nullptr : address));
        if (expected > 0) assert(points.GetAt(expected / 4)->x == oldX + 3.0f);

        auto list = MakeMixedChain(count);
        CheckLinks(list, expected);
        auto head = list.Head;
        auto moved = std::move(list);
        CheckLinks(list, 0);
        CheckLinks(moved, expected);
        assert(moved.Head == head);
        // Destruction and reuse of the moved-from list must not affect the destination.
        list.PushBack(MakeCheckMixedSegment(0, 1));
        CheckLinks(list, 1);
        CheckLinks(moved, expected);

        auto f10Points = MakeF10Points(count);
        assert(f10Points.size() == (size_t)expected + 1);
        assert(f10Points.back().demoX == 6 && f10Points.back().demoY == 6);
    }
    auto f10Points = MakeF10Points(30000);
    assert(f10Points.size() == 30001);
    std::cout << "Demo scenarios: factories, both link directions, moves and boundary sizes passed.\n";
}
