using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;
using API.HR.Node.Services.Workflows;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public class WorkflowEngineTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"WorkflowTestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var dbContext = new ApplicationDbContext(options);
        SeedWorkflowDefinition(dbContext);
        return dbContext;
    }

    private void SeedWorkflowDefinition(ApplicationDbContext dbContext)
    {
        var def = new WorkflowDefinition
        {
            Code = "TEST_WORKFLOW",
            Name = "Test Workflow",
            IsEnabled = true
        };

        var version = new WorkflowVersion
        {
            WorkflowDefinition = def,
            VersionNumber = 1,
            Status = WorkflowVersionStatus.Published
        };
        def.Versions.Add(version);

        var startStep = new WorkflowStep
        {
            WorkflowVersion = version,
            Code = "START",
            Name = "Start Step",
            StepType = WorkflowStepType.Start,
            IsStart = true,
            SortOrder = 1
        };

        var reviewStep = new WorkflowStep
        {
            WorkflowVersion = version,
            Code = "REVIEW",
            Name = "Review Step",
            StepType = WorkflowStepType.HumanTask,
            IsStart = false,
            SortOrder = 2
        };

        var finalStep = new WorkflowStep
        {
            WorkflowVersion = version,
            Code = "COMPLETED",
            Name = "Completed",
            StepType = WorkflowStepType.End,
            IsStart = false,
            IsFinal = true,
            SortOrder = 3
        };

        version.Steps.Add(startStep);
        version.Steps.Add(reviewStep);
        version.Steps.Add(finalStep);

        // Transitions:
        // START -> REVIEW (SUBMIT)
        version.Transitions.Add(new WorkflowTransition
        {
            WorkflowVersion = version,
            FromStep = startStep,
            ToStep = reviewStep,
            ActionCode = "SUBMIT",
            Priority = 1
        });

        // REVIEW -> COMPLETED (APPROVE)
        version.Transitions.Add(new WorkflowTransition
        {
            WorkflowVersion = version,
            FromStep = reviewStep,
            ToStep = finalStep,
            ActionCode = "APPROVE",
            Priority = 1
        });

        dbContext.WorkflowDefinitions.Add(def);
        dbContext.SaveChanges();
    }

    [Fact]
    public async Task StartAsync_ShouldInitializeWorkflow_AndCreateTaskAndTimelineEvent()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var assignmentResolver = new WorkflowAssignmentResolver(dbContext);
        var conditionEvaluator = new WorkflowConditionEvaluator();
        var timelineService = new WorkflowTimelineService(dbContext);
        var engine = new WorkflowEngine(dbContext, assignmentResolver, conditionEvaluator, timelineService, []);

        var command = new StartWorkflowCommand(
            WorkflowCode: "TEST_WORKFLOW",
            EntityType: "TestEntity",
            EntityId: "1001",
            InitiatorUserId: "user1");

        // Act
        var result = await engine.StartAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.WorkflowCode.Should().Be("TEST_WORKFLOW");
        result.CurrentStepCode.Should().Be("REVIEW");
        result.Status.Should().Be(WorkflowInstanceStatus.Running);

        var instance = await dbContext.WorkflowInstances.FirstOrDefaultAsync(i => i.Id == result.WorkflowInstanceId);
        instance.Should().NotBeNull();
        instance!.EntityType.Should().Be("TestEntity");
        instance.EntityId.Should().Be("1001");

        var tasks = await dbContext.WorkflowTasks.ToListAsync();
        tasks.Should().HaveCount(1);
        tasks[0].AssignedUserId.Should().Be("user1");
        tasks[0].Status.Should().Be(WorkflowTaskStatus.Pending);

        var timeline = await timelineService.GetTimelineAsync(instance.Id);
        timeline.Should().NotBeEmpty();
        timeline.Should().Contain(e => e.Type == "WORKFLOW_STARTED");
    }

    [Fact]
    public async Task ExecuteActionAsync_ShouldTransitionToNextStep_WhenActionIsValid()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var assignmentResolver = new WorkflowAssignmentResolver(dbContext);
        var conditionEvaluator = new WorkflowConditionEvaluator();
        var timelineService = new WorkflowTimelineService(dbContext);
        var engine = new WorkflowEngine(dbContext, assignmentResolver, conditionEvaluator, timelineService, []);

        var startResult = await engine.StartAsync(new StartWorkflowCommand("TEST_WORKFLOW", "TestEntity", "1002", "user1"));

        var actionCommand = new ExecuteWorkflowActionCommand(
            WorkflowInstanceId: startResult.WorkflowInstanceId,
            ActionCode: "APPROVE",
            UserId: "user1",
            Comment: "Approving request");

        // Act
        var actionResult = await engine.ExecuteActionAsync(actionCommand);

        // Assert
        actionResult.Should().NotBeNull();
        actionResult.FromStepCode.Should().Be("REVIEW");
        actionResult.ToStepCode.Should().Be("COMPLETED");
        actionResult.ExecutedAction.Should().Be("APPROVE");
        actionResult.Status.Should().Be(WorkflowInstanceStatus.Completed);

        var instance = await dbContext.WorkflowInstances.FirstOrDefaultAsync(i => i.Id == startResult.WorkflowInstanceId);
        instance!.Status.Should().Be(WorkflowInstanceStatus.Completed);
        instance.CompletedAt.Should().NotBeNull();

        var timeline = await timelineService.GetTimelineAsync(instance.Id);
        timeline.Should().Contain(e => e.Type == "APPROVED");
        timeline.Should().Contain(e => e.Type == "WORKFLOW_COMPLETED");
    }

    [Fact]
    public async Task ExecuteActionAsync_ShouldEnforceIdempotency_WhenSameKeyIsSent()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var assignmentResolver = new WorkflowAssignmentResolver(dbContext);
        var conditionEvaluator = new WorkflowConditionEvaluator();
        var timelineService = new WorkflowTimelineService(dbContext);
        var engine = new WorkflowEngine(dbContext, assignmentResolver, conditionEvaluator, timelineService, []);

        var startResult = await engine.StartAsync(new StartWorkflowCommand("TEST_WORKFLOW", "TestEntity", "1003", "user1"));

        var actionCommand = new ExecuteWorkflowActionCommand(
            WorkflowInstanceId: startResult.WorkflowInstanceId,
            ActionCode: "APPROVE",
            UserId: "user1",
            Comment: "First attempt",
            IdempotencyKey: "unique-key-12345");

        // Act
        var firstResult = await engine.ExecuteActionAsync(actionCommand);
        var secondResult = await engine.ExecuteActionAsync(actionCommand);

        // Assert
        firstResult.Should().NotBeNull();
        secondResult.Should().NotBeNull();
        secondResult.ExecutedAction.Should().Be(firstResult.ExecutedAction);
        secondResult.ToStepCode.Should().Be(firstResult.ToStepCode);

        var idempotencyRecords = await dbContext.WorkflowIdempotencyRecords.ToListAsync();
        idempotencyRecords.Should().HaveCount(1);
    }

    [Fact]
    public async Task WorkContinuationWorkflow_FullCycle_ShouldCompleteAndIssueOutboxNotification()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var assignmentResolver = new WorkflowAssignmentResolver(dbContext);
        var conditionEvaluator = new WorkflowConditionEvaluator();
        var timelineService = new WorkflowTimelineService(dbContext);
        var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<WorkContinuationWorkflowHandler>>();
        var handler = new WorkContinuationWorkflowHandler(dbContext, loggerMock.Object);
        var engine = new WorkflowEngine(dbContext, assignmentResolver, conditionEvaluator, timelineService, new[] { handler });

        // Seed WORK_CONTINUATION_REQUEST definition manually in InMemory DB
        var def = new WorkflowDefinition { Code = "WORK_CONTINUATION_REQUEST", Name = "طلب تأييد استمرار بالعمل", IsEnabled = true };
        var ver = new WorkflowVersion { WorkflowDefinition = def, VersionNumber = 1, Status = WorkflowVersionStatus.Published };
        def.Versions.Add(ver);

        var stepStart = new WorkflowStep { WorkflowVersion = ver, Code = "SUBMITTED", Name = "تقديم الطلب", StepType = WorkflowStepType.Start, IsStart = true, SortOrder = 1 };
        var stepFormation = new WorkflowStep { WorkflowVersion = ver, Code = "FORMATION_DIRECTOR_APPROVAL", Name = "موافقة مدير التشكيل", StepType = WorkflowStepType.Approval, SortOrder = 2 };
        var stepHrDirector = new WorkflowStep { WorkflowVersion = ver, Code = "HR_DIRECTOR_APPROVAL", Name = "موافقة مدير الموارد البشرية", StepType = WorkflowStepType.Approval, SortOrder = 3 };
        var stepHrIssuance = new WorkflowStep { WorkflowVersion = ver, Code = "HR_LETTER_ISSUANCE", Name = "قسم الموارد البشرية - إنشاء وتصدير الكتاب", StepType = WorkflowStepType.HumanTask, SortOrder = 4 };
        var stepCompleted = new WorkflowStep { WorkflowVersion = ver, Code = "COMPLETED", Name = "تم إصدار الكتاب", StepType = WorkflowStepType.End, IsFinal = true, SortOrder = 5 };

        ver.Steps.Add(stepStart); ver.Steps.Add(stepFormation); ver.Steps.Add(stepHrDirector); ver.Steps.Add(stepHrIssuance); ver.Steps.Add(stepCompleted);

        ver.Transitions.Add(new WorkflowTransition { WorkflowVersion = ver, FromStep = stepStart, ToStep = stepFormation, ActionCode = "SUBMIT", Priority = 1 });
        ver.Transitions.Add(new WorkflowTransition { WorkflowVersion = ver, FromStep = stepFormation, ToStep = stepHrDirector, ActionCode = "APPROVE", Priority = 1 });
        ver.Transitions.Add(new WorkflowTransition { WorkflowVersion = ver, FromStep = stepHrDirector, ToStep = stepHrIssuance, ActionCode = "APPROVE", Priority = 1 });
        ver.Transitions.Add(new WorkflowTransition { WorkflowVersion = ver, FromStep = stepHrIssuance, ToStep = stepCompleted, ActionCode = "APPROVE", Priority = 1 });

        dbContext.WorkflowDefinitions.Add(def);
        await dbContext.SaveChangesAsync();

        // 1. Employee Submits Request
        var startResult = await engine.StartAsync(new StartWorkflowCommand("WORK_CONTINUATION_REQUEST", "WorkContinuationRequest", "REQ-999", "employee_1"));
        startResult.CurrentStepCode.Should().Be("FORMATION_DIRECTOR_APPROVAL");

        // 2. Formation Director Approves
        var t1 = await engine.ExecuteActionAsync(new ExecuteWorkflowActionCommand(startResult.WorkflowInstanceId, "APPROVE", "manager_formation", "موافق من قبل مدير التشكيل"));
        t1.ToStepCode.Should().Be("HR_DIRECTOR_APPROVAL");

        // 3. HR Director Approves
        var t2 = await engine.ExecuteActionAsync(new ExecuteWorkflowActionCommand(startResult.WorkflowInstanceId, "APPROVE", "manager_hr", "موافق من قبل مدير الموارد البشرية"));
        t2.ToStepCode.Should().Be("HR_LETTER_ISSUANCE");

        // 4. HR Department Issues Official Letter
        var t3 = await engine.ExecuteActionAsync(new ExecuteWorkflowActionCommand(startResult.WorkflowInstanceId, "APPROVE", "hr_officer", "تم تصدير وإصدار الكتاب برقم 8849/2026"));
        t3.ToStepCode.Should().Be("COMPLETED");
        t3.Status.Should().Be(WorkflowInstanceStatus.Completed);

        // Verify Outbox Notification for Mobile App
        var outboxMessages = await dbContext.WorkflowOutboxMessages.ToListAsync();
        outboxMessages.Should().HaveCount(1);
        outboxMessages[0].MessageType.Should().Be("MOBILE_PUSH_NOTIFICATION");
        outboxMessages[0].PayloadJson.Should().Contain("employee_1");
    }
}
