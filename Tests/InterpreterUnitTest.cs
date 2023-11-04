using NeoWatch.Loading;
using Newtonsoft.Json.Linq;

namespace Tests
{
    public class InterpreterUnitTest
    {
        Interpreter interpreter;

        public InterpreterUnitTest()
        {
            var patterns = new Dictionary<PatternKind, string>()
            {
                { PatternKind.Type, @"(?<type>\w+): (?<parse>.*)" },
                { PatternKind.Segment, @"(?<initialPoint>.*) - (?<finalPoint>.*)" },
                { PatternKind.Arc, @"C: (?<centerPoint>.*) R: (?<radius>.*) AngIni: (?<initialAngle>.*) AngPaso: (?<sweepAngle>.*)" },
                { PatternKind.Circle, @"C: (?<centerPoint>.*) R: (?<radius>.*)" },
                { PatternKind.Point, @"\((?<x>.*),(?<y>.*)\)" }
            };

            var typeKindPairs = new Dictionary<string, PatternKind>()
            {
                { "Pnt", PatternKind.Point },
                { "Seg", PatternKind.Segment },
                { "Arc", PatternKind.Arc },
                { "Cir", PatternKind.Circle }
            };

            interpreter = new Interpreter(patterns, typeKindPairs);
        }

        [Theory]
        [InlineData("Seg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)")]
        [InlineData("Arc: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
        public void GetDrawable_WhenValueIsValid_ReturnsValidDrawable(string value)
        {
            // Arrange

            // Act
            var drawable = interpreter.GetDrawable(value);

            // Assert
            Assert.NotNull(drawable);
            Assert.True(drawable.Box.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void GetDrawable_WhenValueIsEmptyOrNull_ReturnsNull(string value)
        {
            // Arrange

            // Act
            var drawable = interpreter.GetDrawable(value);

            // Assert
            Assert.Null(drawable);
        }

        [Theory]
        [InlineData("eg: Pnt: (0.00,0.00) - Pnt: (100.00,0.00)")]
        [InlineData("Arc1: C: Pnt: (0.00,0.00) R: 10 AngIni: 0 AngPaso: 90")]
        [InlineData("A: ")]
        public void GetDrawable_WhenValueHasInvalidType_ReturnsInvalidDrawable(string value)
        {
            // Arrange

            // Act
            var drawable = interpreter.GetDrawable(value);

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
        public void GetDrawable_WhenValueHasInvalidFields_ReturnsNull(string value)
        {
            // Arrange

            // Act
            var drawable = interpreter.GetDrawable(value);

            // Assert
            Assert.Null(drawable);
        }
    }
}