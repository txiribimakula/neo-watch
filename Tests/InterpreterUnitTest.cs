using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoWatch.Common;
using NeoWatch.Drawing;
using NeoWatch.Geometries;
using NeoWatch.Loading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tests.Mocks;

namespace Tests
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
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawableResult);
                Assert.AreEqual(FeedbackType.OK, drawableResult.Feedback.Type);
                Assert.IsTrue(drawableResult.Data.Box.IsValid);
            }
            [TestMethod]
            [DataRow("")]
            [DataRow(null)]
            [DataRow("Seg: Pnt: 0.00,0.00) - Pnt: (100.00,0.00)")]
            [DataRow("Seg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00")]
            [DataRow("Arc: C: Pnt: (0.00,0.00) R: 10 AngIi: 0 AngPaso: 90")]
            [DataRow("A")]
            [DataRow("A:")]
            public void returns_expression_load_error_when_parse_ends_throwing_exception(string value, string parse = nameof(COMException))
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
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(FeedbackType.ExpressionLoadException, drawableResult.Feedback.Type);
            }

            [TestMethod]
            [DataRow(null, "")]
            [DataRow(null, null)]
            [DataRow(null, "Seg: Pnt: 0.00,0.00) - Pnt: (100.00,0.00)")]
            [DataRow(null, "Seg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00")]
            [DataRow(null, "Arc: C: Pnt: (0.00,0.00) R: 10 AngIi: 0 AngPaso: 90")]
            [DataRow(null, "A")]
            [DataRow(null, "A:")]
            public void returns_expression_pattern_missmatch(string value, string parse = nameof(COMException))
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
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNull(drawableResult.Data);
                Assert.AreEqual(FeedbackType.ExpressionPatternMissmatch, drawableResult.Feedback.Type);
            }

            [TestMethod]
            [DataRow("eg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)", "eg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)")]
            [DataRow("Arc1: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90", "Arc1: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
            [DataRow("A: ", "A: ")]
            [DataRow(null, "eg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)")]
            [DataRow(null, "Arc1: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
            [DataRow(null, "A: ")]
            public void returns_typenotfound_when_value_failed_and_parse_has_invalid_type(string value, string parse)
            {
                // Arrange
                ExpressionMock.GetParse getParse = () => parse;
                var expressionMock = new ExpressionMock(value, type: "any", getParse);

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(FeedbackType.TypeNotFound, drawableResult.Feedback.Type);
                Assert.IsNull(drawableResult.Data);
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
                var drawableResultWithLegacyPattern = interpreter.GetDrawable(expressionMockWithLegacyPattern);
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(drawableResult.Data, drawableResultWithLegacyPattern.Data);
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
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawableResult);
                Assert.IsTrue(drawableResult.Data.Box.IsValid);
                var expectedDrawablePoint = new DrawablePoint(5, 15);
                Assert.AreEqual(expectedDrawablePoint, drawableResult.Data);
            }

            [TestMethod]
            public void returns_expected_point_with_negative_numbers()
            {
                // Arrange
                var expressionMock = new ExpressionMock("Pnt: (-5.00,-15.00)", type: "any", () => throw new COMException());

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawableResult);
                Assert.IsTrue(drawableResult.Data.Box.IsValid);
                var expectedDrawablePoint = new DrawablePoint(-5, -15);
                Assert.AreEqual(expectedDrawablePoint, drawableResult.Data);
            }

            [TestMethod]
            [DataRow("Pnt: (5,00,15.00)")]
            [DataRow("Pnt: (5.00,15,00)")]
            [DataRow("Pnt: ((5.00,15.00)")]
            [DataRow("Pnt: 5.00,15.00)")]
            [DataRow("Pnt: (,)")]
            [DataRow("(5.00,15.00)")]
            public void returns_expression_pattern_missmatch(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => value);

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(FeedbackType.ExpressionPatternMissmatch, drawableResult.Feedback.Type);
            }
            
            [TestMethod]
            [DataRow("Pnt: (999999999999999999999999999999999999999999999999999999999999999999999999,15.00)")]
            [DataRow("Pnt: (5.00,999999999999999999999999999999999999999999999999999999999999999999999999)")]
            public void does_not_overflow(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => throw new COMException());

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                // implicit if no exception is thrown
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
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawableResult);
                Assert.IsTrue(drawableResult.Data.Box.IsValid);
                var expectedDrawableLineSegment = new DrawableLineSegment(new Point(5, 15), new Point(100, 10));
                Assert.AreEqual(expectedDrawableLineSegment, drawableResult.Data);
            }


            [TestMethod]
            [DataRow("Seg: (5.00,15.00) (100.00,10.00)")]
            [DataRow("Pnt: (5.00,15.00) - Pnt: (100.00,10.00)")]
            public void returns_expression_pattern_missmatch(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => value);

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(FeedbackType.ExpressionPatternMissmatch, drawableResult.Feedback.Type);
            }


            [TestMethod]
            [DataRow("Segment: Pnt: (5.00,15.00) - Pnt: (100.00,10.00)")]
            public void returns_invalid_type_error(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => value);

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(FeedbackType.TypeNotFound, drawableResult.Feedback.Type);
            }

            [TestMethod]
            [DataRow("Seg: (999999999999999999999999999999999999999999999999999999999999999999999999,15.00) - (100.00,10.00)")]
            [DataRow("Seg: (5.00,999999999999999999999999999999999999999999999999999999999999999999999999) - (100.00,10.00)")]
            [DataRow("Seg: (5.00,15.00) - (999999999999999999999999999999999999999999999999999999999999999999999999,10.00)")]
            [DataRow("Seg: (5.00,15.00) - (100.00,999999999999999999999999999999999999999999999999999999999999999999999999)")]
            public void does_not_overflow(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => throw new COMException());

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                // implicit if no exception is thrown
            }
        }

        [TestClass]
        public class Get_Drawable_Arc
        {
            IInterpreter interpreter;

            public Get_Drawable_Arc()
            {
                interpreter = new Interpreter(patterns, typeKindPairs);
            }

            [TestMethod]
            public void returns_expected_arc()
            {
                // Arrange
                var expressionMock = new ExpressionMock("Arc: C: (0.00,0.00) R: 10.00 AngIni: 0.00 AngPaso: 90.00", type: "any", () => throw new COMException());

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.IsNotNull(drawableResult);
                Assert.IsTrue(drawableResult.Data.Box.IsValid);
                var expectedDrawableArcSegment = new DrawableArcSegment(new Point(0, 0), 0, 90, 10);
                Assert.AreEqual(expectedDrawableArcSegment, drawableResult.Data);
            }

            [TestMethod]
            [DataRow("Arc: C: (0.00,0.00) R: AngIni: 0.00 AngPaso: 90.00")]
            [DataRow("Arc: C: (0.00,0.00) AngIni: 0.00 AngPaso: 90.00")]
            public void returns_expression_pattern_missmatch(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => value);

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(FeedbackType.ExpressionPatternMissmatch, drawableResult.Feedback.Type);
            }

            [TestMethod]
            [DataRow("NotAnArc: C: (0.00,0.00) R: 10.00 AngIni: 0.00 AngPaso: 90.00")]
            public void returns_invalid_type_error(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => value);

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.AreEqual(FeedbackType.TypeNotFound, drawableResult.Feedback.Type);
            }

            [TestMethod]
            [DataRow("Arc: C: (999999999999999999999999999999999999999999999999999999999999999999999999,0.00) R: 10.00 AngIni: 0.00 AngPaso: 90.00")]
            [DataRow("Arc: C: (0.00,999999999999999999999999999999999999999999999999999999999999999999999999) R: 10.00 AngIni: 0.00 AngPaso: 90.00")]
            [DataRow("Arc: C: (0.00,0.00) R: 999999999999999999999999999999999999999999999999999999999999999999999999 AngIni: 0.00 AngPaso: 90.00")]
            [DataRow("Arc: C: (0.00,0.00) R: 10.00 AngIni: 999999999999999999999999999999999999999999999999999999999999999999999999 AngPaso: 90.00")]
            [DataRow("Arc: C: (0.00,0.00) R: 10.00 AngIni: 0.00 AngPaso: 999999999999999999999999999999999999999999999999999999999999999999999999")]
            public void does_not_overflow(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value, type: "any", () => throw new COMException());

                // Act
                var drawableResult = interpreter.GetDrawable(expressionMock);

                // Assert
                // implicit if no exception is thrown
            }
        }
    }
}
