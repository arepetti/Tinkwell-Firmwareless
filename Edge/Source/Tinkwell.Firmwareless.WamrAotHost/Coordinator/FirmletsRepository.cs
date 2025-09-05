using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Tinkwell.Firmwareless.WamrAotHost.Coordinator;

sealed class FirmletsRepository
{
    public IEnumerable<HostInfo> Hosts => _hosts.Values;

    public IEnumerable<HostInfo> ActiveHosts
        => _hosts.Values.Where(x => x.Process is not null && !x.Process.HasExited);

    public int Count => _hosts.Count;

    public bool IsEmpty => _hosts.IsEmpty;

    public void Add(IEnumerable<FirmletEntry> hosts)
    {
        _hosts = new ConcurrentDictionary<string, HostInfo>(
            hosts
                .Select(entry => new HostInfo(entry.Id, IdHelpers.CreateId("firmlet", 12), entry.Path))
                .ToDictionary(x => x.Id, x => x)
        );
    }

    public bool TryGetByHostId(string id, [NotNullWhen(true)] out HostInfo host)
        => _hosts.TryGetValue(id, out host!);

    public bool TryGetByProcessId(int pid, [NotNullWhen(true)] out HostInfo host) 
    {
        host = _hosts?.Values.FirstOrDefault(x => x.Process is not null && !x.Process.HasExited && x.Process?.Id == pid)!;
        return host is not null;
    }

    public bool TryGetByExternalReferenceId(string externalReferenceId, [NotNullWhen(true)] out HostInfo host)
    {
        host = _hosts?.Values.FirstOrDefault(x => x.ExternalReferenceId == externalReferenceId)!;
        return host is not null;
    }

    private ConcurrentDictionary<string, HostInfo> _hosts = new();
}
