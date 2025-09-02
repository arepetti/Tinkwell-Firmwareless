namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

interface IWamrHost
{
    void Load(string[] paths);
    void InitializeModules();
    void Start();

    void Stop();
}
