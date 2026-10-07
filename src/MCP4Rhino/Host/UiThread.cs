using System.Collections.Concurrent;

namespace MCP4Rhino.Host;

/// <summary>
/// Marshals work onto Rhino's UI thread and waits for the result.
/// </summary>
public static class UiThread
{
    public static T Invoke<T>(Func<T> func)
    {
        if (Rhino.RhinoApp.InvokeRequired == false)
            return func();

        var bag = new ConcurrentBag<(T? Value, Exception? Error)>();
        using var done = new ManualResetEventSlim(false);

        Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
        {
            try
            {
                bag.Add((func(), null));
            }
            catch (Exception ex)
            {
                bag.Add((default, ex));
            }
            finally
            {
                done.Set();
            }
        }));

        done.Wait();
        var (value, error) = bag.First();
        if (error is not null)
            throw error;
        return value!;
    }

    public static void Invoke(Action action) =>
        Invoke(() =>
        {
            action();
            return true;
        });
}
