using System.Diagnostics;

namespace DataAccess.Entity.Logistics.GA.Runs;

using System.Diagnostics;

public class ResourceMonitor
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Stopwatch _stopwatch = new();

    private readonly List<double> _cpuSamples = new();
    private readonly List<long> _memorySamples = new();

    private TimeSpan _lastCpuTime;
    private DateTime _lastCheck;

    public void Start()
    {
        _process.Refresh();

        _lastCpuTime = _process.TotalProcessorTime;
        _lastCheck = DateTime.UtcNow;

        _stopwatch.Start();
    }

    public void Tick()
    {
        _process.Refresh();

        var now = DateTime.UtcNow;
        var cpuTime = _process.TotalProcessorTime;

        var cpuUsedMs = (cpuTime - _lastCpuTime).TotalMilliseconds;
        var elapsedMs = (now - _lastCheck).TotalMilliseconds;

        var cpuPercent = elapsedMs > 0
            ? cpuUsedMs / (Environment.ProcessorCount * elapsedMs) * 100
            : 0;

        _cpuSamples.Add(cpuPercent);
        _memorySamples.Add(_process.WorkingSet64);

        _lastCpuTime = cpuTime;
        _lastCheck = now;
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public double GetAverageCpuUsage() =>
        _cpuSamples.Count == 0 ? 0 : _cpuSamples.Average();

    public long GetMaxMemoryMb() =>
        _memorySamples.Count == 0 ? 0 : _memorySamples.Max() / (1024 * 1024);

    public long GetElapsedMilliseconds() =>
        _stopwatch.ElapsedMilliseconds;
}