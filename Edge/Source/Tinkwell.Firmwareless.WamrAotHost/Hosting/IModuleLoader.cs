namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

interface IModuleLoader
{
    void Load(string[] paths);
    void InitializeModules();
}
