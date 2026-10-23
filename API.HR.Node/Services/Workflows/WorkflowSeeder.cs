using API.HR.Node.Data;
using API.HR.Node.Data.Workflows;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Services.Workflows;

public static class WorkflowSeeder
{
    public static async Task SeedWorkflowsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Seed Pilot 1: APPRECIATION_LETTER
        var appDef = await dbContext.WorkflowDefinitions
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Code == "APPRECIATION_LETTER");

        if (appDef == null)
        {
            appDef = new WorkflowDefinition
            {
                Code = "APPRECIATION_LETTER",
                Name = "مسار اعتماد ومصادقة كتب الشكر والتقدير",
                Description = "مسار عام متعدد المراحل لإصدار واعتماد كتب الشكر والتقدير للموظفين",
                FormSchemaJson = "[{\"name\":\"reason\",\"label\":\"سبب منح كتاب الشكر\",\"type\":\"text\",\"required\":true,\"placeholder\":\"مثال: تميز وإنجاز العمل بفترة قياسية\"},{\"name\":\"grantedBy\",\"label\":\"الجهة أو التشكيل المانح\",\"type\":\"text\",\"required\":true,\"placeholder\":\"مثال: مكتب الوزير / المدير العام\"}]",
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "systemadmin"
            };
            dbContext.WorkflowDefinitions.Add(appDef);
            await dbContext.SaveChangesAsync();
        }
        else if (string.IsNullOrWhiteSpace(appDef.FormSchemaJson))
        {
            appDef.FormSchemaJson = "[{\"name\":\"reason\",\"label\":\"سبب منح كتاب الشكر\",\"type\":\"text\",\"required\":true,\"placeholder\":\"مثال: تميز وإنجاز العمل بفترة قياسية\"},{\"name\":\"grantedBy\",\"label\":\"الجهة أو التشكيل المانح\",\"type\":\"text\",\"required\":true,\"placeholder\":\"مثال: مكتب الوزير / المدير العام\"}]";
            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.WorkflowVersions.AnyAsync(v => v.WorkflowDefinitionId == appDef.Id && v.Status == WorkflowVersionStatus.Published))
        {
            var version = new WorkflowVersion
            {
                WorkflowDefinitionId = appDef.Id,
                VersionNumber = 1,
                Status = WorkflowVersionStatus.Published,
                ChangeDescription = "النسخة الأولية المعتمدة لنظام كتب الشكر",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "systemadmin",
                PublishedAt = DateTimeOffset.UtcNow,
                PublishedBy = "systemadmin"
            };

            var stepDraft = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "DRAFT",
                Name = "مسودة كتاب الشكر",
                Description = "إعداد وتعبئة تفاصيل كتاب الشكر",
                StepType = WorkflowStepType.Start,
                IsStart = true,
                IsFinal = false,
                SortOrder = 1
            };

            var stepReview = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "DEPARTMENT_REVIEW",
                Name = "مراجعة واستشعار القسم المعني",
                Description = "تدقيق صحة البيانات والتثبت من الأوراق الرسمية",
                StepType = WorkflowStepType.Review,
                IsStart = false,
                IsFinal = false,
                SortOrder = 2,
                ConfigurationJson = "{\"assignmentType\":\"INITIATOR\"}"
            };

            var stepApproval = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "DIRECTOR_APPROVAL",
                Name = "مصادقة واعتماد المدير العام",
                Description = "الموافقة النهائية والتوقيع الإداري",
                StepType = WorkflowStepType.Approval,
                IsStart = false,
                IsFinal = false,
                SortOrder = 3,
                ConfigurationJson = "{\"assignmentType\":\"INITIATOR\"}"
            };

            var stepCompleted = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "COMPLETED",
                Name = "مكتمل ومعتمد رسمياً",
                Description = "تم اعتماد الكتاب وإدراجه في ملف الموظف",
                StepType = WorkflowStepType.End,
                IsStart = false,
                IsFinal = true,
                SortOrder = 4
            };

            var stepRejected = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "REJECTED",
                Name = "مرفوض",
                Description = "تم رفض إصدار كتاب الشكر",
                StepType = WorkflowStepType.End,
                IsStart = false,
                IsFinal = true,
                SortOrder = 5
            };

            version.Steps.Add(stepDraft);
            version.Steps.Add(stepReview);
            version.Steps.Add(stepApproval);
            version.Steps.Add(stepCompleted);
            version.Steps.Add(stepRejected);

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepDraft,
                ToStep = stepReview,
                ActionCode = "SUBMIT",
                Priority = 1
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepReview,
                ToStep = stepApproval,
                ActionCode = "APPROVE",
                Priority = 1
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepReview,
                ToStep = stepRejected,
                ActionCode = "REJECT",
                Priority = 2
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepReview,
                ToStep = stepDraft,
                ActionCode = "RETURN",
                Priority = 3
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepApproval,
                ToStep = stepCompleted,
                ActionCode = "APPROVE",
                Priority = 1
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepApproval,
                ToStep = stepRejected,
                ActionCode = "REJECT",
                Priority = 2
            });

            dbContext.WorkflowVersions.Add(version);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded published version for workflow 'APPRECIATION_LETTER' successfully.");
        }

        // Seed Workflow 2: WORK_CONTINUATION_REQUEST
        var workContDef = await dbContext.WorkflowDefinitions
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Code == "WORK_CONTINUATION_REQUEST");

        if (workContDef == null)
        {
            workContDef = new WorkflowDefinition
            {
                Code = "WORK_CONTINUATION_REQUEST",
                Name = "طلب تأييد استمرار بالعمل",
                Description = "مسار تقديم ومعالجة طلبات التأييد الرسمية وإصدار كتاب الاستمرار بالعمل وإشعار الموظف على الهاتف",
                FormSchemaJson = "[{\"name\":\"purpose\",\"label\":\"الغرض أو سبب كتاب التأييد\",\"type\":\"text\",\"required\":true,\"placeholder\":\"مثال: تقديم للبنك العقاري / المصرف\"},{\"name\":\"directToEntity\",\"label\":\"الجهة الموجه إليها الكتاب\",\"type\":\"text\",\"required\":true,\"placeholder\":\"مثال: المصرف العقاري العراقي\"},{\"name\":\"urgency\",\"label\":\"درجة الأسبقية\",\"type\":\"select\",\"options\":[\"عادي\",\"عاجل\",\"عاجل جداً\"],\"required\":true}]",
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "systemadmin"
            };
            dbContext.WorkflowDefinitions.Add(workContDef);
            await dbContext.SaveChangesAsync();
        }
        else if (string.IsNullOrWhiteSpace(workContDef.FormSchemaJson))
        {
            workContDef.FormSchemaJson = "[{\"name\":\"purpose\",\"label\":\"الغرض أو سبب كتاب التأييد\",\"type\":\"text\",\"required\":true,\"placeholder\":\"مثال: تقديم للبنك العقاري / المصرف\"},{\"name\":\"directToEntity\",\"label\":\"الجهة الموجه إليها الكتاب\",\"type\":\"text\",\"required\":true,\"placeholder\":\"مثال: المصرف العقاري العراقي\"},{\"name\":\"urgency\",\"label\":\"درجة الأسبقية\",\"type\":\"select\",\"options\":[\"عادي\",\"عاجل\",\"عاجل جداً\"],\"required\":true}]";
            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.WorkflowVersions.AnyAsync(v => v.WorkflowDefinitionId == workContDef.Id && v.Status == WorkflowVersionStatus.Published))
        {
            var version = new WorkflowVersion
            {
                WorkflowDefinitionId = workContDef.Id,
                VersionNumber = 1,
                Status = WorkflowVersionStatus.Published,
                ChangeDescription = "النسخة الأولى لمسار تأييد الاستمرار بالعمل",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "systemadmin",
                PublishedAt = DateTimeOffset.UtcNow,
                PublishedBy = "systemadmin"
            };

            var stepStart = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "SUBMITTED",
                Name = "تقديم الطلب",
                Description = "إرسال طلب التأييد من قبل الموظف",
                StepType = WorkflowStepType.Start,
                IsStart = true,
                IsFinal = false,
                SortOrder = 1
            };

            var stepFormationDirector = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "FORMATION_DIRECTOR_APPROVAL",
                Name = "موافقة مدير التشكيل",
                Description = "تدقيق وموافقة مدير التشكيل المعني",
                StepType = WorkflowStepType.Approval,
                IsStart = false,
                IsFinal = false,
                SortOrder = 2,
                ConfigurationJson = "{\"assignmentType\":\"MANAGER_OF_EMPLOYEE\"}"
            };

            var stepHrDirector = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "HR_DIRECTOR_APPROVAL",
                Name = "موافقة مدير الموارد البشرية",
                Description = "مصادقة مدير قسم الموارد البشرية",
                StepType = WorkflowStepType.Approval,
                IsStart = false,
                IsFinal = false,
                SortOrder = 3,
                ConfigurationJson = "{\"assignmentType\":\"ROLE\", \"roleId\":\"HrDirector\"}"
            };

            var stepHrIssuance = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "HR_LETTER_ISSUANCE",
                Name = "قسم الموارد البشرية - إنشاء وتصدير الكتاب",
                Description = "طباعة وتصدير كتاب التأييد الرسمي وتوليد الأرقام الرسمية",
                StepType = WorkflowStepType.HumanTask,
                IsStart = false,
                IsFinal = false,
                SortOrder = 4,
                ConfigurationJson = "{\"assignmentType\":\"DEPARTMENT\", \"departmentId\":1}"
            };

            var stepCompleted = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "COMPLETED",
                Name = "تم إصدار الكتاب وإشعار الموظف",
                Description = "صدور الكتاب رسمياً وإرسال إشعار فوري لهاتف الموظف",
                StepType = WorkflowStepType.End,
                IsStart = false,
                IsFinal = true,
                SortOrder = 5
            };

            var stepRejected = new WorkflowStep
            {
                WorkflowVersion = version,
                Code = "REJECTED",
                Name = "مرفوض",
                Description = "رفض طلب التأييد",
                StepType = WorkflowStepType.End,
                IsStart = false,
                IsFinal = true,
                SortOrder = 6
            };

            version.Steps.Add(stepStart);
            version.Steps.Add(stepFormationDirector);
            version.Steps.Add(stepHrDirector);
            version.Steps.Add(stepHrIssuance);
            version.Steps.Add(stepCompleted);
            version.Steps.Add(stepRejected);

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepStart,
                ToStep = stepFormationDirector,
                ActionCode = "SUBMIT",
                Priority = 1
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepFormationDirector,
                ToStep = stepHrDirector,
                ActionCode = "APPROVE",
                Priority = 1
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepFormationDirector,
                ToStep = stepRejected,
                ActionCode = "REJECT",
                Priority = 2
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepHrDirector,
                ToStep = stepHrIssuance,
                ActionCode = "APPROVE",
                Priority = 1
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepHrDirector,
                ToStep = stepRejected,
                ActionCode = "REJECT",
                Priority = 2
            });

            version.Transitions.Add(new WorkflowTransition
            {
                WorkflowVersion = version,
                FromStep = stepHrIssuance,
                ToStep = stepCompleted,
                ActionCode = "APPROVE",
                Priority = 1
            });

            dbContext.WorkflowVersions.Add(version);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded published version for workflow 'WORK_CONTINUATION_REQUEST' successfully.");
        }
    }
}
