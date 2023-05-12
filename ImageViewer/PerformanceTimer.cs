using System;
using System.Diagnostics;

namespace ImageViewer;

public class PerformanceTimer : IDisposable
{
    private readonly string _message;
    private readonly Stopwatch _timer;

    public PerformanceTimer(string message)
    {
        _message = message;
        _timer = new Stopwatch();
        _timer.Start();
    }
    
    public void Dispose()
    {
        _timer.Stop();
        Debug.WriteLine($"{_message} ({_timer.ElapsedMilliseconds}ms)");
    }
}