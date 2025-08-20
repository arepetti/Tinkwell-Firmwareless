using Microsoft.Extensions.Hosting;

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

// Database
var storage = builder.AddAzureStorage("tinkwell-firmwarestore");
if (builder.Environment.IsDevelopment())
{
    storage.RunAsEmulator(resource =>
    {
        resource.WithBindMount(Path.Combine(dataRoot, "azurite"), "/data");
    });
}

var assets = storage.AddBlobs("tinkwell-firmwarestore-assets");
var builds = storage.AddBlobs("tinkwell-firmwarestore-builds");

// Blob
var pg = builder.AddPostgres("tinkwell-firmwaredb");
var manifestsDb = pg.AddDatabase("tinkwell-firmwaredb-manifests");
if (builder.Environment.IsDevelopment())
    pg = pg.WithBindMount(Path.Combine(dataRoot, "pgdata"), "/var/lib/postgresql/data");

// Compilation service
var wamrcCompiler = builder.AddDockerfile("wamrc-compiler", "../Tinkwell.Firmwareless.WamrcCompiler");

var compilationServer = builder
    .AddProject<Projects.Tinkwell_Firmwareless_CompilationServer>("tinkwell-compilation-server")
    .WithReference(assets)
    .WithReference(builds)
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
    .WithExternalHttpEndpoints();

// Done, run it!
builder.Build().Run();
