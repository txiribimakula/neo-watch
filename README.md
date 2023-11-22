# Getting started

## NatVis

[Create custom views of C++ objects in the debugger using the Natvis framework](https://learn.microsoft.com/en-us/visualstudio/debugger/create-custom-views-of-native-objects?view=vs-2022)

❗️Always define a ***Parse*** field that matches any of the patterns.

❗️Use the same pattern on the DisplayString.

Table of supported types with their default patterns:

| Type | Pattern |
|-|-|
| Point | `Pnt: ({x},{y})` |
| Line | `Seg: {initialPoint} - {finalPoint}` |
| Arc | `Arc: C: {centerPoint} R: {radius} AngIni: {initialAngle} AngPaso: {sweepAngle}` |
| List | `List` |

[Check Wiki for more insigths on NatVis](https://github.com/txiribimakula/neo-watch/wiki/NatVis)

## Installation

*Extensions* > *Manage Extensions...* > *Search: **Neo Watch***

# Usage

*Debug* > *Windows* > ***Neo Watch***

![alt text](demo.gif "Title")
