#pragma once
class DemoListOfItself
{
public:
    DemoListOfItself* Next, * Previous;

    float x;
    float y;
};

// Vista con raiz y tamano para observar la cadena como una coleccion sin cambiar
// la propiedad de sus nodos, que sigue estando en el escenario de la demo.
class DemoPointLinkedList
{
public:
    DemoPointLinkedList(DemoListOfItself* head, int count);

    DemoListOfItself* Head;
    int Count;

    void Reset(DemoListOfItself* head, int count);
    DemoListOfItself* GetAt(int index);
};
