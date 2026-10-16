using MCP4Rhino.Logic.Grasshopper;
using Xunit;

namespace MCP4Rhino.Tests;

public class GrasshopperJobTests
{
    [Fact]
    public void SolvesAreQueuedAndDoNotRunDuringSubmission()
    {
        var ui = new Queue<Action>(); using var jobs = new SolveQueue(ui.Enqueue);
        var executed = new List<int>();
        var first = jobs.Enqueue("s", _ => { executed.Add(1); return "one"; });
        var second = jobs.Enqueue("s", _ => { executed.Add(2); return "two"; });
        Assert.Empty(executed); Assert.Equal("queued", first.State); Assert.True(jobs.Busy);
        ui.Dequeue()(); Assert.Equal(new[] { 1 }, executed);
        Assert.Equal("completed", jobs.Get(first.JobId).State);
        Assert.Equal("queued", jobs.Get(second.JobId).State);
        ui.Dequeue()(); ui.Dequeue()();
        Assert.Equal(new[] { 1, 2 }, executed); Assert.False(jobs.Busy);
    }

    [Fact]
    public void QueuedCancellationSkipsExecutionAndRunningCancellationRetainsSlot()
    {
        var ui = new Queue<Action>(); using var jobs = new SolveQueue(ui.Enqueue);
        string? runningId = null;
        var first = jobs.Enqueue("s", c =>
        {
            Assert.Equal("running", jobs.Get(runningId!).State);
            jobs.Cancel(runningId!); Assert.True(jobs.Busy); Assert.True(c.CancellationRequested);
            return "must not publish";
        });
        runningId = first.JobId;
        var cancelled = jobs.Enqueue("s", _ => throw new Exception("must not execute"));
        Assert.Equal("cancelled", jobs.Cancel(cancelled.JobId).State);
        ui.Dequeue()(); Assert.Equal("cancelled", jobs.Get(first.JobId).State); Assert.Null(jobs.Get(first.JobId).Result);
        ui.Dequeue()(); Assert.False(jobs.Busy);
        Assert.Equal("cancelled", jobs.Cancel(first.JobId).State);
    }

    [Fact]
    public void FailureDoesNotLoseTheNextJobAndUnknownJobsAreExplicit()
    {
        var ui = new Queue<Action>(); using var jobs = new SolveQueue(ui.Enqueue);
        var bad = jobs.Enqueue("s", _ => throw new InvalidOperationException("component failed"));
        var good = jobs.Enqueue("s", _ => "success");
        ui.Dequeue()(); ui.Dequeue()(); ui.Dequeue()();
        Assert.Equal("failed", jobs.Get(bad.JobId).State); Assert.Equal("component failed", jobs.Get(bad.JobId).Error);
        Assert.Equal("success", jobs.Get(good.JobId).Result);
        Assert.Throws<GhException>(() => jobs.Get("missing")); Assert.Throws<GhException>(() => jobs.Cancel("missing"));
    }

    [Fact]
    public void DeadlineRequestsAbortWithoutPublishingOrOverlappingWork()
    {
        var ui = new Queue<Action>(); using var jobs = new SolveQueue(ui.Enqueue);
        using var abort = new ManualResetEventSlim();
        var first = jobs.Enqueue("s", c =>
        {
            c.Abort = abort.Set;
            Assert.True(abort.Wait(TimeSpan.FromSeconds(4)));
            Assert.True(c.TimedOut); Assert.True(jobs.Busy);
            return "late result";
        }, timeoutSeconds: 1);
        ui.Dequeue()();
        Assert.Equal("timed_out", jobs.Get(first.JobId).State); Assert.Null(jobs.Get(first.JobId).Result);
        ui.Dequeue()(); Assert.False(jobs.Busy);
    }

    [Fact]
    public void CapacityAndDisposalPreventUnboundedPendingWork()
    {
        var ui = new Queue<Action>(); var jobs = new SolveQueue(ui.Enqueue);
        var first = jobs.Enqueue("s", _ => "unused");
        for (var i = 0; i < 31; i++) jobs.Enqueue("s", _ => "unused");
        Assert.Throws<GhException>(() => jobs.Enqueue("s", _ => "overflow"));
        jobs.Dispose(); Assert.Equal("cancelled", jobs.Get(first.JobId).State);
        ui.Dequeue()(); Assert.False(jobs.Busy);
        Assert.Throws<ObjectDisposedException>(() => jobs.Enqueue("s", _ => "disposed"));
    }

    [Fact]
    public void OldTerminalJobsArePrunedButPendingJobsRemain()
    {
        var ui = new Queue<Action>(); using var jobs = new SolveQueue(ui.Enqueue);
        var first = jobs.Enqueue("s", _ => "old");
        while (ui.TryDequeue(out var action)) action();
        for (var i = 0; i < 256; i++)
        {
            var job = jobs.Enqueue("s", _ => "done");
            while (ui.TryDequeue(out var action)) action();
            Assert.Equal("completed", jobs.Get(job.JobId).State);
        }
        Assert.Throws<GhException>(() => jobs.Get(first.JobId));
    }
}
