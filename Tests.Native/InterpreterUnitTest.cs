using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Drawing;
using NeoWatch.Geometries;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tests.Mocks;

namespace Native.Tests
{
    [TestClass]
    public class InterpreterUnitTest
    {
        private static Dictionary<PatternKind, string[]> patterns = new Dictionary<PatternKind, string[]>()
        {
            { PatternKind.Type, new string[] { @"(?<type>\w+): (?<parse>.*)" } },
            { PatternKind.Segment, new string[] { @"(?<initialPoint>.*) - (?<finalPoint>.*)" } },
            { PatternKind.Arc, new string[] { @"C: (?<centerPoint>.*) R: (?<radius>.*) AngIni: (?<initialAngle>.*) AngPaso: (?<sweepAngle>.*)" } },
            { PatternKind.Circle, new string[] { @"C: (?<centerPoint>.*) R: (?<radius>.*)" } },
            { PatternKind.Point, new string[] { @"^\((?<x>-?\d*\.?\d+),(?<y>-?\d*\.?\d+)\)$" } }
            //{ PatternKind.Point, new string[] { @"^\((?<x>[+\-]?(?=[\,\.]\d|\d)(?:0|[1-9]\d*)?(?:[\,\.]\d+)?(?:(?<=\d)(?:[eE][+\-]?\d+))?),(?<y>[+\-]?(?=[\,\.]\d|\d)(?:0|[1-9]\d*)?(?:[\,\.]\d+)?(?:(?<=\d)(?:[eE][+\-]?\d+))?)\)$" } }
        };

        private static Dictionary<string, PatternKind> typeKindPairs = new Dictionary<string, PatternKind>()
        {
            { "Pnt", PatternKind.Point },
            { "Seg", PatternKind.Segment },
            { "Arc", PatternKind.Arc },
            { "Cir", PatternKind.Circle }
        };

        [TestClass]
        public class Get_Drawable
        {
            Interpreter interpreter;

            public Get_Drawable()
            {
                interpreter = new Interpreter(patterns, typeKindPairs);
            }

            [TestMethod]
            [DataRow("Seg: (0.00,0.00) - (100.00,0.00)")]
            [DataRow("Arc: C: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
            [DataRow(null, "Seg: (0.00,0.00) - (100.00,0.00)")]
            [DataRow(null, "Arc: C: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
            public void returns_valid_drawable_when_expression_value_and_or_parse_are_valid(string value, string parse = nameof(COMException))
            {
                // Arrange
                ExpressionMock.GetParse getParse;
                if(parse == nameof(COMException))
                {
                    getParse = () => throw new COMException();
                }
                else
                {
                    getParse = () => parse;
                }
                var expressionMock = new ExpressionMock(value, type: "any", getParse);

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawable);
                Assert.IsTrue(drawable.Box.IsValid);
            }

            [TestMethod]
            [DataRow("")]
            [DataRow(null)]
            [DataRow(null, "")]
            [DataRow(null, null)]
            public void returns_null_when_expression_has_no_value_and_no_parse(string value, string parse = nameof(COMException))
            {
                // Arrange
                ExpressionMock.GetParse getParse;
                if (parse == nameof(COMException))
                {
                    getParse = () => throw new COMException();
                }
                else
                {
                    getParse = () => parse;
                }
                var expressionMock = new ExpressionMock(value, type: "any", getParse);

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNull(drawable);
            }

            [TestMethod]
            [DataRow("eg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)")]
            [DataRow("Arc1: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
            [DataRow("A: ")]
            [DataRow(null, "eg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)")]
            [DataRow(null, "Arc1: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
            [DataRow(null, "A: ")]
            public void returns_invalid_drawable_when_value_and_or_parse_have_invalid_type(string value, string parse = nameof(COMException))
            {
                // Arrange
                ExpressionMock.GetParse getParse;
                if (parse == nameof(COMException))
                {
                    getParse = () => throw new COMException();
                }
                else
                {
                    getParse = () => parse;
                }
                var expressionMock = new ExpressionMock(value, type: "any", getParse);

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual("Type is not interpretable.", drawable.Description);
                Assert.IsNull(drawable.Box);
            }

            [TestMethod]
            [DataRow("Seg: Pnt: 0.00,0.00) - Pnt: (100.00,0.00)")]
            [DataRow("Seg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00")]
            [DataRow("Arc: C: Pnt: (0.00,0.00) R: 10 AngIi: 0 AngPaso: 90")]
            [DataRow("A")]
            [DataRow("A:")]
            [DataRow(null, "Seg: Pnt: 0.00,0.00) - Pnt: (100.00,0.00)")]
            [DataRow(null, "Seg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00")]
            [DataRow(null, "Arc: C: Pnt: (0.00,0.00) R: 10 AngIi: 0 AngPaso: 90")]
            [DataRow(null, "A")]
            [DataRow(null, "A:")]
            public void returns_null_when_value_and_parse_have_invalid_fields(string value, string parse = nameof(COMException))
            {
                // Arrange
                ExpressionMock.GetParse getParse;
                if (parse == nameof(COMException))
                {
                    getParse = () => throw new COMException();
                }
                else
                {
                    getParse = () => parse;
                }
                var expressionMock = new ExpressionMock(value, type: "any", getParse);

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNull(drawable);
            }
        }

        [TestClass]
        public class Get_Drawable_With_Retrocompatible_Pattern
        {
            Interpreter interpreter;

            public Get_Drawable_With_Retrocompatible_Pattern()
            {
                var patternsClone = patterns.ToDictionary(entry => entry.Key, entry => {
                    if (entry.Key == PatternKind.Point)
                    {
                        return new string[]
                        {
                            entry.Value[0],
                            @"\((?<x>.*),(?<y>.*)\)"
                        };
                    }
                    return entry.Value;
                });
                interpreter = new Interpreter(patternsClone, typeKindPairs);
            }

            [TestMethod]
            public void returns_same_drawables()
            {
                // Arrange
                var expressionMockWithLegacyPattern = new ExpressionMock("Seg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)", type: "any", () => throw new COMException());
                var expressionMock = new ExpressionMock("Seg: (0.00,0.00) - (100.00,0.00)", type: "any", () => throw new COMException());

                // Act
                var drawableWithLegacyPattern = interpreter.GetDrawable(expressionMockWithLegacyPattern);
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(drawable, drawableWithLegacyPattern);
            }
        }

        [TestClass]
        public class Get_Drawable_Point
        {
            Interpreter interpreter;

            public Get_Drawable_Point()
            {
                interpreter = new Interpreter(patterns, typeKindPairs);
            }

            [TestMethod]
            public void returns_expected_point()
            {
                // Arrange
                var expressionMock = new ExpressionMock("Pnt: (5.00,15.00)", type: "any", () => throw new COMException());

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawable);
                Assert.IsTrue(drawable.Box.IsValid);
                var expectedDrawablePoint = new DrawablePoint(5, 15);
                Assert.AreEqual(expectedDrawablePoint, drawable);
            }

            [TestMethod]
            public void returns_expected_point_with_negative_numbers()
            {
                // Arrange
                var expressionMock = new ExpressionMock("Pnt: (-5.00,-15.00)", type: "any", () => throw new COMException());

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawable);
                Assert.IsTrue(drawable.Box.IsValid);
                var expectedDrawablePoint = new DrawablePoint(-5, -15);
                Assert.AreEqual(expectedDrawablePoint, drawable);
            }

            [TestMethod]
            [DataRow("Pnt: (5,00,15.00)")]
            [DataRow("Pnt: (5.00,15,00)")]
            [DataRow("Pnt: ((5.00,15.00)")]
            [DataRow("Pnt: 5.00,15.00)")]
            [DataRow("Pnt: (,)")]
            [DataRow("(5.00,15.00)")]
            public void returns_null(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => throw new COMException());

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNull(drawable);
            }
        }

        [TestClass]
        public class Get_Drawable_Segment
        {
            Interpreter interpreter;

            public Get_Drawable_Segment()
            {
                interpreter = new Interpreter(patterns, typeKindPairs);
            }

            [TestMethod]
            public void returns_expected_segment()
            {
                // Arrange
                var expressionMock = new ExpressionMock("Seg: (5.00,15.00) - (100.00,10.00)", type: "any", () => throw new COMException());

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawable);
                Assert.IsTrue(drawable.Box.IsValid);
                var expectedDrawableLineSegment = new DrawableLineSegment(new Point(5, 15), new Point(100, 10));
                Assert.AreEqual(expectedDrawableLineSegment, drawable);
            }


            [TestMethod]
            [DataRow("Seg: (5.00,15.00) (100.00,10.00)")]
            [DataRow("Pnt: (5.00,15.00) - Pnt: (100.00,10.00)")]
            public void returns_null(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => throw new COMException());

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNull(drawable);
            }


            [TestMethod]
            [DataRow("Segment: Pnt: (5.00,15.00) - Pnt: (100.00,10.00)")]
            public void returns_invalid_type_error(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => throw new COMException());

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual("Type is not interpretable.", drawable.Description);
            }
        }
    }
}
