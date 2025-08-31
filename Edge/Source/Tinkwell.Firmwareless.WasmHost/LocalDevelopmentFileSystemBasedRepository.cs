using System.Text;

namespace Tinkwell.Firmwareless.WasmHost
{
    sealed class LocalDevelopmentFileSystemBasedRepository : IPublicRepository
    {
        public Task<string> GetPublicKeyAsync(CancellationToken cancellationToken)
        {
            var baseDirectory = Path.Combine(AppContext.BaseDirectory, "LocalRepository");
            var path = Path.Combine(baseDirectory, "public_key.pem");
            if (!File.Exists(path))
                throw new FileNotFoundException("Public key file not found", path);

            return Task.FromResult(File.ReadAllText(path, Encoding.UTF8));
        }
    }
}