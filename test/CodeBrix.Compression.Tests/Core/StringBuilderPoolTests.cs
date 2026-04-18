using CodeBrix.Compression.Core;
using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Compression.Tests.Core;

[Trait("Category", "Core")]
public class StringBuilderPoolTests
{
    [Fact]
    public void RoundTrip()
    {
        var pool = new StringBuilderPool();
        var builder1 = pool.Rent();
        pool.Return(builder1);
        var builder2 = pool.Rent();
        Assert.Equal(builder1, builder2);
    }

    [Fact]
    public void ReturnsClears()
    {
        var pool = new StringBuilderPool();
        var builder1 = pool.Rent();
        builder1.Append("Hello");
        pool.Return(builder1);
        Assert.Equal(0, builder1.Length);
    }

    [Fact]
    public async Task ThreadSafeAsync()
    {
        var concurrency = 100;

        var pool = new StringBuilderPool();
        var gate = new TaskCompletionSource<bool>();
        var startedTasks = new Task[concurrency];
        var completedTasks = new Task<string>[concurrency];
        for (var i = 0; i < concurrency; i++)
        {
            var started = new TaskCompletionSource<bool>();
            startedTasks[i] = started.Task;
            var captured = i;
            completedTasks[i] = Task.Run(async () =>
            {
                started.SetResult(true);
                await gate.Task;
                var builder = pool.Rent();
                builder.Append("Hello ");
                builder.Append(captured);
                var str = builder.ToString();
                pool.Return(builder);
                return str;
            });
        }

        await Task.WhenAll(startedTasks);

        gate.SetResult(true);

        var results = await Task.WhenAll(completedTasks);
        for (var i = 0; i < concurrency; i++)
        {
            var result = results[i];
            Assert.Equal($"Hello {i}", result);
        }
    }
}
