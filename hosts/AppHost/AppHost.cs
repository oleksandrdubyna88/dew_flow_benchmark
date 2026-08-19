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
// The password is a PARAMETER, not Aspire's per-run generated one, and this is not a preference —
// it is a defect this file already had. A generated password is regenerated on every start, while the
// data volume below is persistent, and postgres only reads POSTGRES_PASSWORD when it INITIALISES a
// cluster. So the second run of this AppHost hands the daemon a password the existing cluster has
// never heard of, and every connection fails with "password authentication failed for user postgres"
// against a database that is running perfectly. Observed here on the second run.
//
// Parameters resolve from configuration and persist across runs. The dev value lives in this project's
// launchSettings.json — dev-only, never published, the same precedent dew_flow_rag_qln uses for its
// Neo4j password — and a real deployment overrides it with `dotnet user-secrets set
// Parameters:bench-postgres-password <value>` or the matching environment variable. Marked secret so
// it is redacted in the dashboard rather than printed beside the connection string.
//
// launchSettings also sets DOTNET_ENVIRONMENT=Development, and that part is load-bearing: without it
// the AppHost starts in Production, user secrets are not loaded at all, and an unresolved parameter
// leaves the orchestrator waiting with a running dashboard and no containers — which looks like a hang
// rather than a missing value. Observed here before the file existed.
var password = builder.AddParameter("bench-postgres-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: password)
    .WithImage("postgres", "17.10")
    .WithDataVolume("bench-postgres-data")
    // Persistent: the container belongs to the DATA, not to one AppHost session. A benchmark's whole value
    // is that a measurement taken in March is still comparable in August.
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerRuntimeArgs("--label", $"com.docker.compose.project={DockerGroup}");

var database = postgres.AddDatabase("bench");

// The read surface, and the only project resource here. The CLI is still NOT one, and that is honest
// rather than unfinished: it is a command an agent runs, not a service an orchestrator supervises, and it
// takes its connection string from --connection or ConnectionStrings__bench.
//
// This host serves reports and starts nothing. `bench run` remains the one verb that reaches a model, so
// nothing an orchestrator restarts can begin spending money — and a start button waits for the console's
// own worker and the accelerator lease that makes two drains against one card safe
// (todo/PLAN_variant_matrix.md step 6a).
//
// It applies no migrations: the CLI owns the schema, and two processes racing Migrate() against one
// database is a defect rather than a race worth winning. Against an empty database this host answers
// "no run" until a `bench run` has created it.
builder.AddProject<Projects.Api>("bench-api")
    .WithReference(database)
    .WaitFor(database);

builder.Build().Run();
