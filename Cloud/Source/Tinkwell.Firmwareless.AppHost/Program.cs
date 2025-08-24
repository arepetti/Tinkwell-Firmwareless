using Microsoft.Extensions.Hosting;
using Aspire.Hosting.Azure;

var builder = DistributedApplication.CreateBuilder(args);

// Persist containers' data locally in development, we do not want to setup everything each time.
// If we need to start over then simply delete the ".containers" directory.
string dataRoot = "";
if (builder.Environment.IsDevelopment())
{
    dataRoot = Path.Combine(builder.AppHostDirectory ?? Directory.GetCurrentDirectory(), ".containers");
    Directory.CreateDirectory(Path.Combine(dataRoot, "pgdata"));
    Directory.CreateDirectory(Path.Combine(dataRoot, "azurite"));
}

// Assets
var storage = builder.AddAzureStorage("tinkwell-firmwarestore");
if (builder.Environment.IsDevelopment())
{
    storage.RunAsEmulator(resource =>
    {
        resource.WithBindMount(Path.Combine(dataRoot, "azurite"), "/data");
    });
}

// Currently .NET Aspire does not support read-only and write-only access to these resources,
// currently we *COULD* create two SAS tokens with:
//   az storage container generate-sas --name tinkwell-firmwarestore-assets --permissions r --expiry ... --account-name ... --https-only
// and then manually build and inject the connection string with .WithEnvironment("BlobConnectionString", "...") but that's exactly
// what I want to avoid using .NET Aspire. For a PoC it's not a big deal but it might be a good thing to do in production.

var assets = storage.AddBlobs("tinkwell-firmwarestore-assets");
var builds = storage.AddBlobs("tinkwell-firmwarestore-builds");

// DB
var pg = builder.AddPostgres("tinkwell-firmwaredb");
var manifestsDb = pg.AddDatabase("tinkwell-firmwaredb-manifests");
if (builder.Environment.IsDevelopment())
    pg = pg.WithBindMount(Path.Combine(dataRoot, "pgdata"), "/var/lib/postgresql/data");

// Key Vault
var keyVault = builder.AddAzureKeyVault("tinkwell-keyvault");

// Compilation service
var wamrcCompiler = builder.AddDockerfile("wamrc-compiler", "../Tinkwell.Firmwareless.WamrcCompiler");

var compilationServer = builder
    .AddProject<Projects.Tinkwell_Firmwareless_CompilationServer>("tinkwell-compilation-server")
    .WithReference(assets)
    .WithReference(builds)
    .WithReference(keyVault)
    .WithHttpsEndpoint();

if (wamrcCompiler.Resource.TryGetContainerImageName(out var compilerImageName))
    compilationServer.WithEnvironment("CompilerImageName", compilerImageName);
else
    throw new InvalidOperationException("Cannot obtain the compiler image name from the wamrc-compiler project.");

// Public repository
var publicRepository = builder
    .AddProject<Projects.Tinkwell_Firmwareless_PublicRepository>("tinkwell-public-repository")
    .WithReference(assets)
    .WithReference(manifestsDb)
    .WithReference(compilationServer)
    .WithReference(keyVault)
    .WithExternalHttpEndpoints();

// Done, run it!
builder.Build().Run();
