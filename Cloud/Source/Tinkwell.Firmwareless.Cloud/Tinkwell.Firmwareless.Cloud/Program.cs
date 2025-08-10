using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("tinkwell-firmwarestore");
if (builder.Environment.IsDevelopment())
    storage.RunAsEmulator();

var assets = storage.AddBlobs("tinkwell-firmwarestore-assets");
var builds = storage.AddBlobs("tinkwell-firmwarestore-builds");

var pg = builder.AddPostgres("tinkwell-firmwaredb");
var manifestsDb = pg.AddDatabase("tinkwell-firmwaredb-manifests");

var publicRepository = builder
    .AddProject<Projects.Tinkwell_Firmwareless_PublicRepository>("tinkwell-public-repository")
    .WithReference(assets)
    .WithReference(builds)
    .WithReference(manifestsDb)
    .WithExternalHttpEndpoints();

var compilationServer = builder
    .AddProject<Projects.Tinkwell_Firmwareless_CompilationServer>("tinkwell-compilation-server")
    .WithReference(builds)
    .WithReference(manifestsDb);

builder
    .Build()
    .Run();
