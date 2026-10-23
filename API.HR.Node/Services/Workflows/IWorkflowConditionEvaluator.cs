using API.HR.Node.Data.Workflows;

namespace API.HR.Node.Services.Workflows;

public interface IWorkflowConditionEvaluator
{
    bool EvaluateConditions(
        IEnumerable<WorkflowCondition> conditions,
        Dictionary<string, object>? context);
}
