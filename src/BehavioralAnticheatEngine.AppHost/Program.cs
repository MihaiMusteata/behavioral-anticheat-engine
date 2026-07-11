Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

var builder = DistributedApplication.CreateBuilder(args);

var api = builder
    .AddProject<Projects.BehavioralAnticheatEngine_Api>("api", launchProfileName: "http")
    .WithExternalHttpEndpoints()
    .WithEnvironment("DOTNET_USE_POLLING_FILE_WATCHER", "1");

builder
    .AddExecutable("client", "npm", "../../client", "run", "dev", "--", "--host", "0.0.0.0")
    .WithHttpEndpoint(targetPort: 5173, port: 5173, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("CHOKIDAR_USEPOLLING", "true")
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
