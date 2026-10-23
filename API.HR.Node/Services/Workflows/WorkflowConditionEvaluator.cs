using System.Globalization;
using API.HR.Node.Data.Workflows;

namespace API.HR.Node.Services.Workflows;

public class WorkflowConditionEvaluator : IWorkflowConditionEvaluator
{
    public bool EvaluateConditions(
        IEnumerable<WorkflowCondition> conditions,
        Dictionary<string, object>? context)
    {
        var conditionList = conditions.ToList();
        if (conditionList.Count == 0) return true;
        if (context == null || context.Count == 0) return false;

        foreach (var condition in conditionList)
        {
            if (!context.TryGetValue(condition.FieldName, out var actualValRaw))
            {
                return false;
            }

            var actualValStr = actualValRaw?.ToString() ?? string.Empty;
            var targetValStr = condition.Value ?? string.Empty;

            var isMatch = EvaluateSingle(actualValStr, condition.Operator, targetValStr, actualValRaw);
            if (!isMatch) return false;
        }

        return true;
    }

    private static bool EvaluateSingle(string actual, string op, string target, object? rawActual)
    {
        switch (op.Trim())
        {
            case "==":
            case "=":
                return string.Equals(actual, target, StringComparison.OrdinalIgnoreCase);

            case "!=":
                return !string.Equals(actual, target, StringComparison.OrdinalIgnoreCase);

            case ">":
                return CompareNumbers(actual, target) > 0;

            case ">=":
                return CompareNumbers(actual, target) >= 0;

            case "<":
                return CompareNumbers(actual, target) < 0;

            case "<=":
                return CompareNumbers(actual, target) <= 0;

            case "Contains":
                return actual.Contains(target, StringComparison.OrdinalIgnoreCase);

            case "In":
                var items = target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return items.Any(i => string.Equals(i, actual, StringComparison.OrdinalIgnoreCase));

            default:
                return false;
        }
    }

    private static int CompareNumbers(string a, string b)
    {
        if (decimal.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out var numA) &&
            decimal.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out var numB))
        {
            return numA.CompareTo(numB);
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
