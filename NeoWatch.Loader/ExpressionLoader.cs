using EnvDTE;
using System.Runtime.InteropServices;

namespace NeoWatch.Loading
{
    public class ExpressionLoading
    {
        public ExpressionLoading(Expression expression) {
            this.expression = expression;
            Type = expression.Type;
            Name = expression.Name;
        }

        private Expression expression;
        public string Type { get; set; }
        public string Name { get; set; }

        public ExpressionLoading GetMember(params string[] names) {
            Expression expression = this.expression;
            foreach (var name in names) {
                try {
                    expression = expression.DataMembers.Item(name);
                } catch(COMException ex) {
                    throw new MemberNotFoundException(expression.Type, name);
                }
            }
            return new ExpressionLoading(expression);
        }

        public float GetFloatValue(params string[] names) {
            Expression expression = this.expression;
            foreach (var name in names) {
                expression = expression.DataMembers.Item(name);
            }
            return float.Parse(expression.Value, System.Globalization.NumberStyles.Float, System.Threading.Thread.CurrentThread.CurrentUICulture);
        }

        public string GetStringValue(params string[] names) {
            Expression expression = this.expression;
            foreach (var name in names) {
                expression = expression.DataMembers.Item(name);
            }
            return expression.Value;
        }

        public ExpressionLoading[] GetMembers() {
            EnvDTE.Expressions expressions = expression.DataMembers;
            ExpressionLoading[] expressionsLoadings = new ExpressionLoading[expressions.Count];
            int i = 0;
            foreach (Expression expression in expressions) {
                expressionsLoadings[i] = new ExpressionLoading(expression);
                i++;
            }
            return expressionsLoadings;
        }
    }
} 
