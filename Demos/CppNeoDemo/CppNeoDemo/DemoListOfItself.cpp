#include "DemoListOfItself.h"

DemoPointLinkedList::DemoPointLinkedList(DemoListOfItself* head, int count)
    : Head(head),
      Count(count)
{
}

void DemoPointLinkedList::Reset(DemoListOfItself* head, int count)
{
    Head = count > 0 ? head : nullptr;
    Count = count > 0 ? count : 0;
}

DemoListOfItself* DemoPointLinkedList::GetAt(int index)
{
    if (index < 0 || index >= Count)
    {
        return nullptr;
    }

    DemoListOfItself* current = Head;
    for (int i = 0; i < index && current != nullptr; i++)
    {
        current = current->Next;
    }

    return current;
}
