using Bench.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = DistributedApplication.CreateBuilder(args);

// The orchestrator logs like everything it starts, and its own file is what says WHY a child never came
// up — a start that did not happen leaves no log in the child.
//
// Through the factory rather than the host extension: an orchestrator's builder is not an
// IHostApplicationBuilder, and the alternative was a second copy of every logging decision here.
Log.Logger = BenchLogging.CreateLogger(builder.Configuration, builder.AppHostDirectory, "bench-apphost");
builder.Services.AddLogging(logging => logging.ClearProviders().AddSerilog(Log.Logger, dispose: true));

// Docker Desktop groups containers by the Compose project label. Stamping this AppHost's containers with
// their own value keeps them a separate group from every product stack on the machine.
//
// That separation is the point rather than tidiness. This machine already runs the product's Postgres
// (group "dew-flow-qln") and its Qdrant, plus a third-party Qdrant on the default port. A benchmark that
// shared any of them would be measuring a database somebody else is writing to — and its own results
// would be one `docker compose down` away from belonging to another product's lifecycle.
const string DockerGroup = "dew-flow-bench";

// PostgreSQL — this benchmark's own store, and nothing else's. PostgreSQL License.
//
// Pinned by TAG ONLY, deliberately: Aspire derives the data-volume mount target from the postgres VERSION
// parsed out of the image tag, and a digest pin CLEARS the tag — leaving no version to parse, mounting the
// volume one level above PGDATA, and initdb'ing an empty cluster while the real data sits unmounted in the
// volume. Measured in a sibling repository, and the failure looks like "the database lost everything".
var postgres = builder.AddPostgres("postgres")
    .WithImage("postgres", "17.10")
    .WithDataVolume("bench-postgres-data")
    // Persistent: the container belongs to the DATA, not to one AppHost session. A benchmark's whole value
    // is that a measurement taken in March is still comparable in August.
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerRuntimeArgs("--label", $"com.docker.compose.project={DockerGroup}");

var database = postgres.AddDatabase("bench");

// No project resources yet, and that is honest rather than unfinished: the CLI is not a service an
// orchestrator supervises — it is a command an agent runs, and it takes its connection string from
// --connection or ConnectionStrings__bench. What this AppHost provides is the database that string points
// at. The API host joins here when it exists.
builder.Build().Run();
