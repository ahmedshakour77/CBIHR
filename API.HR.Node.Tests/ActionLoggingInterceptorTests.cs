using API.HR.Node.Services.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace API.HR.Node.Tests;

public class ActionLoggingInterceptorTests
{
    [Fact]
    public async Task LogsSavedChangesWithoutPropertyValues()
    {
        var logger = new Mock<ILogger<ActionLoggingInterceptor>>();
        var interceptor = new ActionLoggingInterceptor(logger.Object, new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<TestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor).Options;
        await using var db = new TestContext(options);
        var item = new TestItem { Id = 7, Name = "private value" };
        db.Add(item);
        await db.SaveChangesAsync();
        item.Name = "another private value";
        await db.SaveChangesAsync();
        db.Remove(item);
        await db.SaveChangesAsync();

        var messages = logger.Invocations.Select(i => i.Arguments[2]?.ToString() ?? "").ToArray();
        Assert.Contains(messages, m => m.Contains("Added TestItem") && m.Contains("Key=7"));
        Assert.Contains(messages, m => m.Contains("Modified TestItem") && m.Contains("Fields=Name"));
        Assert.Contains(messages, m => m.Contains("Deleted TestItem"));
        Assert.DoesNotContain(messages, m => m.Contains("private value"));
    }

    private sealed class TestContext(DbContextOptions<TestContext> options) : DbContext(options)
    {
        public DbSet<TestItem> Items => Set<TestItem>();
    }
    private sealed class TestItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
