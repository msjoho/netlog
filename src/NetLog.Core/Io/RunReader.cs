using NetLog.Core.Csv;
using NetLog.Core.Models;

namespace NetLog.Core.Io;

public static class RunReader
{
    /// <summary>
    /// Loads a run folder: run.json plus every host under hosts\&lt;name&gt;\ (each with its
    /// optional _status.json and connections.csv). Hosts are ordered by name for determinism.
    /// </summary>
    public static RunData Read(string runFolder)
    {
        var config = RunConfigReader.ReadFile(Path.Combine(runFolder, "run.json"));

        var hosts = new List<HostData>();
        var hostsRoot = Path.Combine(runFolder, "hosts");
        if (Directory.Exists(hostsRoot))
        {
            foreach (var dir in Directory.GetDirectories(hostsRoot).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(dir);
                var status = HostStatusReader.ReadFile(Path.Combine(dir, "_status.json"));
                var conns = CsvConnectionReader.ReadFile(Path.Combine(dir, "connections.csv"));
                hosts.Add(new HostData(name, status, conns));
            }
        }

        return new RunData(config, hosts);
    }
}
