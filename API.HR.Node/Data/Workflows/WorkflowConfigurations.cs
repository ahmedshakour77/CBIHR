using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace API.HR.Node.Data.Workflows;

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.Code).IsUnique();

        builder.HasMany(d => d.Versions)
            .WithOne(v => v.WorkflowDefinition)
            .HasForeignKey(v => v.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.HasIndex(v => new { v.WorkflowDefinitionId, v.VersionNumber }).IsUnique();

        builder.HasMany(v => v.Steps)
            .WithOne(s => s.WorkflowVersion)
            .HasForeignKey(s => s.WorkflowVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Transitions)
            .WithOne(t => t.WorkflowVersion)
            .HasForeignKey(t => t.WorkflowVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.WorkflowVersionId, s.Code }).IsUnique();
    }
}

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.HasKey(t => t.Id);

        builder.HasOne(t => t.FromStep)
            .WithMany()
            .HasForeignKey(t => t.FromStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToStep)
            .WithMany()
            .HasForeignKey(t => t.ToStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Conditions)
            .WithOne(c => c.Transition)
            .HasForeignKey(c => c.TransitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.HasKey(i => i.Id);

        // Fast lookup by business entity
        builder.HasIndex(i => new { i.EntityType, i.EntityId });
        builder.HasIndex(i => new { i.WorkflowVersionId, i.Status });
        builder.HasIndex(i => i.InitiatorUserId);

        builder.HasOne(i => i.WorkflowVersion)
            .WithMany()
            .HasForeignKey(i => i.WorkflowVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.CurrentStep)
            .WithMany()
            .HasForeignKey(i => i.CurrentStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Tasks)
            .WithOne(t => t.WorkflowInstance)
            .HasForeignKey(t => t.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Events)
            .WithOne(e => e.WorkflowInstance)
            .HasForeignKey(e => e.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Comments)
            .WithOne(c => c.WorkflowInstance)
            .HasForeignKey(c => c.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowTaskConfiguration : IEntityTypeConfiguration<WorkflowTask>
{
    public void Configure(EntityTypeBuilder<WorkflowTask> builder)
    {
        builder.HasKey(t => t.Id);

        builder.HasIndex(t => new { t.AssignedUserId, t.Status });
        builder.HasIndex(t => new { t.AssignedDepartmentId, t.Status });
        builder.HasIndex(t => new { t.AssignedRoleId, t.Status });

        builder.HasOne(t => t.WorkflowStep)
            .WithMany()
            .HasForeignKey(t => t.WorkflowStepId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowEventConfiguration : IEntityTypeConfiguration<WorkflowEvent>
{
    public void Configure(EntityTypeBuilder<WorkflowEvent> builder)
    {
        builder.HasKey(e => e.Id);

        // Index for rapid timeline queries
        builder.HasIndex(e => new { e.WorkflowInstanceId, e.OccurredAt });
    }
}

public class WorkflowIdempotencyRecordConfiguration : IEntityTypeConfiguration<WorkflowIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowIdempotencyRecord> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.IdempotencyKey).IsUnique();
    }
}
