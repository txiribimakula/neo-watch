using NeoWatch.Loading;
using Tests.Mocks;

namespace Tests
{
    public class InterpreterUnitTest
    {
        private static Dictionary<PatternKind, string> patterns = new Dictionary<PatternKind, string>()
        {
            { PatternKind.Type, @"(?<type>\w+): (?<parse>.*)" },
            { PatternKind.Segment, @"(?<initialPoint>.*) - (?<finalPoint>.*)" },
            { PatternKind.Arc, @"C: (?<centerPoint>.*) R: (?<radius>.*) AngIni: (?<initialAngle>.*) AngPaso: (?<sweepAngle>.*)" },
            { PatternKind.Circle, @"C: (?<centerPoint>.*) R: (?<radius>.*)" },
            { PatternKind.Point, @"\((?<x>.*),(?<y>.*)\)" }
        };

        private static Dictionary<string, PatternKind> typeKindPairs = new Dictionary<string, PatternKind>()
        {
            { "Pnt", PatternKind.Point },
            { "Seg", PatternKind.Segment },
            { "Arc", PatternKind.Arc },
            { "Cir", PatternKind.Circle }
        };

        public class Get_Drawable
        {
            Interpreter interpreter;

            public Get_Drawable()
            {
                interpreter = new Interpreter(patterns, typeKindPairs);
            }

            [Theory]
            [InlineData("Seg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)")]
            [InlineData("Arc: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
            public void returns_valid_drawable_when_expression_value_is_valid(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value);

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.NotNull(drawable);
                Assert.True(drawable.Box.IsValid);
            }

            [Theory]
            [InlineData("")]
            [InlineData(null)]
            public void returns_null_when_expression_value_is_empty(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value);

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.Null(drawable);
            }

            [Theory]
            [InlineData("eg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)")]
            [InlineData("Arc1: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
            [InlineData("A: ")]
            public void returns_invalid_drawable_when_value_has_invalid_type(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value);

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.Equal("Type is not interpretable.", drawable.Description);
                Assert.Null(drawable.Box);
            }

            [Theory]
            [InlineData("Seg: Pnt: 0.00,0.00) - Pnt: (100.00,0.00)")]
            [InlineData("Seg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00")]
            [InlineData("Arc: C: Pnt: (0.00,0.00) R: 10 AngIi: 0 AngPaso: 90")]
            [InlineData("A")]
            [InlineData("A:")]
            public void returns_null_when_value_has_invalid_fields(string value)
            {
                // Arrange
                var expressionMock = new ExpressionMock(value);

                // Act
                var drawable = interpreter.GetDrawable(expressionMock);

                // Assert
                Assert.Null(drawable);
            }
        }
    }
}
