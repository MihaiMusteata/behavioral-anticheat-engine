Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "1");
Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

var builder = DistributedApplication.CreateBuilder(args);
var apiProjectPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "BehavioralAnticheatEngine.Api", "BehavioralAnticheatEngine.Api.csproj"));
var workerProjectPath = Path.GetFullPath(
    Path.Combine(builder.AppHostDirectory, "..", "BehavioralAnticheatEngine.Worker", "BehavioralAnticheatEngine.Worker.csproj"));
var clientPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", "client"));
var kafkaExternalPort = FindAvailableTcpPort(19092);
var kafkaBootstrapServers = $"localhost:{kafkaExternalPort}";
const string behavioralEventsTopic = "behavioral-events";
const string workerConsumerGroup = "behavioral-anticheat-worker";

// Dev-only placeholder defaults - fine for local use, but override via
// user secrets / environment variables (not committed) before ever exposing
// this stack through a public tunnel for a real exam.
var accessTokenSigningKey = builder.Configuration["Jwt:AccessToken:SigningKey"]
    ?? "development-access-token-signing-key-change-before-production";
var refreshTokenSigningKey = builder.Configuration["Jwt:RefreshToken:SigningKey"]
    ?? "development-refresh-token-signing-key-change-before-production";
var seedAdminPassword = builder.Configuration["SeedUsers:AdminPassword"] ?? "Admin123!";
var seedStudentPassword = builder.Configuration["SeedUsers:StudentPassword"] ?? "Student123!";

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("behavioral-anticheat-postgres-data");
var database = postgres.AddDatabase("appdb", "behavioral_anticheat");

var redis = builder
    .AddRedis("redis")
    .WithDataVolume("behavioral-anticheat-redis-data");

var redpanda = builder
    .AddContainer("redpanda", "redpandadata/redpanda", "latest")
    .WithArgs(
        "redpanda",
        "start",
        "--overprovisioned",
        "--smp",
        "1",
        "--memory",
        "512M",
        "--reserve-memory",
        "0M",
        "--node-id",
        "0",
        "--check=false",
        "--kafka-addr",
        "PLAINTEXT://0.0.0.0:9092,OUTSIDE://0.0.0.0:19092",
        "--advertise-kafka-addr",
        $"PLAINTEXT://redpanda:9092,OUTSIDE://{kafkaBootstrapServers}")
    .WithEndpoint(name: "kafka", targetPort: 19092, port: kafkaExternalPort, isProxied: false)
    .WithHttpEndpoint(name: "admin", targetPort: 9644)
    .WithVolume("behavioral-anticheat-redpanda-data", "/var/lib/redpanda/data");

builder
    .AddContainer("redpanda-console", "docker.redpanda.com/redpandadata/console", "v3.10.0")
    .WithEnvironment("KAFKA_BROKERS", "redpanda:9092")
    .WithEnvironment("REDPANDA_ADMINAPI_ENABLED", "true")
    .WithEnvironment("REDPANDA_ADMINAPI_URLS", "http://redpanda:9644")
    .WithHttpEndpoint(name: "http", targetPort: 8080, port: 8081, isProxied: false)
    .WithExternalHttpEndpoints()
    .WaitFor(redpanda);

var api = builder
    .AddProject("api", apiProjectPath, launchProfileName: "http")
    .WithExternalHttpEndpoints()
    .WithEnvironment("DOTNET_USE_POLLING_FILE_WATCHER", "1")
    .WithEnvironment("ConnectionStrings__DefaultConnection", database)
    .WithEnvironment("Redis__Configuration", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafkaBootstrapServers)
    .WithEnvironment("Kafka__BehavioralEventsTopic", behavioralEventsTopic)
    .WithEnvironment("Kafka__ConsumerGroupId", workerConsumerGroup)
    .WithEnvironment("Jwt__AccessToken__SigningKey", accessTokenSigningKey)
    .WithEnvironment("Jwt__RefreshToken__SigningKey", refreshTokenSigningKey)
    .WithEnvironment("SeedUsers__0__Email", "admin@example.test")
    .WithEnvironment("SeedUsers__0__Password", seedAdminPassword)
    .WithEnvironment("SeedUsers__0__Role", "Admin")
    .WithEnvironment("SeedUsers__1__Email", "student@example.test")
    .WithEnvironment("SeedUsers__1__Password", seedStudentPassword)
    .WithEnvironment("SeedUsers__1__Role", "Student")
    .WithReference(database)
    .WithReference(redis)
    .WaitFor(database)
    .WaitFor(redis)
    .WaitFor(redpanda);

builder
    .AddProject("worker", workerProjectPath)
    .WithEnvironment("DOTNET_USE_POLLING_FILE_WATCHER", "1")
    .WithEnvironment("ConnectionStrings__DefaultConnection", database)
    .WithEnvironment("Redis__Configuration", redis)
    .WithEnvironment("Kafka__BootstrapServers", kafkaBootstrapServers)
    .WithEnvironment("Kafka__BehavioralEventsTopic", behavioralEventsTopic)
    .WithEnvironment("Kafka__ConsumerGroupId", workerConsumerGroup)
    .WithEnvironment("Jwt__AccessToken__SigningKey", accessTokenSigningKey)
    .WithEnvironment("Jwt__RefreshToken__SigningKey", refreshTokenSigningKey)
    .WithReference(database)
    .WithReference(redis)
    .WaitFor(database)
    .WaitFor(redis)
    .WaitFor(redpanda)
    .WaitFor(api);

builder
    .AddExecutable("client", "npm", clientPath, "run", "dev", "--", "--host", "0.0.0.0")
    .WithHttpEndpoint(targetPort: 5173, port: 5173, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints()
    .WithEnvironment("CHOKIDAR_USEPOLLING", "true")
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();

static int FindAvailableTcpPort(int preferredPort)
{
    const int maxTcpPort = 65535;

    for (var port = preferredPort; port <= maxTcpPort; port++)
    {
        if (IsTcpPortAvailable(port))
        {
            return port;
        }
    }

    throw new InvalidOperationException($"No available TCP port found starting from {preferredPort}.");
}

static bool IsTcpPortAvailable(int port)
{
    try
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
        listener.Start();
        return true;
    }
    catch (System.Net.Sockets.SocketException)
    {
        return false;
    }
}
