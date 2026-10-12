# NetKubernetes API

[Léelo en español](README.es.md)

Real estate REST API built with ASP.NET Core 7, featuring JWT authentication and
SQL Server persistence through Entity Framework Core. It runs in containers with
Docker, and includes the Kubernetes manifests from the orchestration exercise.

This is the backend for [edificaciones-store-angular](https://github.com/Andersonporras/edificaciones-store-angular).
I built it as a learning project to walk the full chain of a real API: token
authentication, data access through an ORM, centralized error handling, and
packaging into containers.

## Project Status

I would rather be explicit about what is tested and what is not:

- **Tested**: the API running locally with `dotnet run` and in a container with
  Docker, against SQL Server. Registration, login, the token, and the protected
  endpoints all work.
- **Not tested**: I never deployed this to a real Kubernetes cluster. The manifests in
  [`KubernetesApp/`](KubernetesApp/) are the exercise from the course I followed; they
  are reviewed and corrected, but I have not applied them to a cluster. Going further
  down that road required a cloud account with a credit card and more machine
  resources than I had available, so I stopped there.

I am publishing it anyway because writing the manifests did teach me how a deployment
is reasoned about (see [What I Learned](#what-i-learned)), but I do not want it read
as experience operating clusters, because I do not have that.

## Screenshots

![Swagger UI showing the property and user endpoints, each with its authorization padlock](docs/screenshots/swagger-endpoints.png)

*Swagger UI: the seven endpoints grouped by controller. The padlock marks the ones
requiring a token, and the **Authorize** button lets you paste it in to try them.*

![Login endpoint response returning the JWT token](docs/screenshots/swagger-login.png)

*`POST /api/Usuario/login` with the demo user: the response carries the JWT token that
then goes into the `Authorization` header.*

## Features

- **JWT authentication**: register and login endpoints returning a token signed with HMAC-SHA512.
- **Secure by default**: a global authorization filter requires a token on every
  endpoint; the ones meant to be public are explicitly marked `[AllowAnonymous]`.
- **Property CRUD**: list, get by id, create and delete, with each property tied to
  the user who created it.
- **Centralized error handling**: middleware catches exceptions and always responds
  with JSON and the right HTTP status, instead of leaking stack traces.
- **Migrations on startup**: the app applies EF Core migrations and seeds demo data,
  retrying while the database finishes starting up.
- **Interactive docs**: Swagger UI with an *Authorize* button, so protected endpoints
  can be tested by pasting the token.
- **No secrets in the repository**: the connection string and the token signing key
  are injected through environment variables, user-secrets or a Kubernetes `Secret`.

## Tech Stack

| Area | Technology |
| --- | --- |
| Framework | ASP.NET Core 7 (Web API) |
| Language | C# |
| ORM | Entity Framework Core 6 |
| Database | SQL Server 2022 |
| Authentication | ASP.NET Core Identity + JWT (HMAC-SHA512) |
| DTO mapping | AutoMapper |
| Documentation | Swagger / Swashbuckle (OpenAPI) |
| Containers | Docker (multi-stage build) + Docker Compose |
| Orchestration | Kubernetes — manifests written, not deployed |

Early on I also tried **MySQL** with the Pomelo provider, to see how interchangeable
the engine really is in EF Core. It worked, but I focused on SQL Server; the MySQL
code is left commented in `Program.cs` for reference.

## Architecture

```
NetKubernetes/
├── Controllers/              # HTTP endpoints: receive, delegate, respond
│   ├── InmuebleController.cs
│   └── UsuarioController.cs
├── Data/                     # Data access (repository pattern)
│   ├── AppDbContext.cs       # DbContext on top of IdentityDbContext
│   ├── LoadDatabase.cs       # Seeds the demo user and properties
│   ├── Inmuebles/            # IInmuebleRepository + implementation
│   └── Usuarios/             # IUsuarioRepository + implementation
├── Dtos/                     # API request and response contracts
├── Middleware/               # Global exception handling
├── Migrations/               # EF Core migrations
├── Models/                   # Entities: Inmueble (property), Usuario (user)
├── Profiles/                 # AutoMapper profiles
├── Token/                    # JWT generation and current-user lookup
├── KubernetesApp/            # Kubernetes manifests
├── Dockerfile
├── docker-compose.yml
└── Program.cs                # Composition: DI, authentication, pipeline
```

A request flows like this: the **controller** receives a DTO, the **repository** does
the work against `AppDbContext`, **AutoMapper** turns the entity into a response DTO,
and any exception is intercepted by `ManagerMiddleware` and translated into JSON with
its status code.

Authorization works the opposite way from the usual approach: instead of decorating
what needs protection with `[Authorize]`, `Program.cs` registers a global
`AuthorizeFilter` and public endpoints are marked `[AllowAnonymous]`. That way a new
endpoint is born protected if you forget about it.

> Domain names are in Spanish throughout the code (`Inmueble` = property,
> `Usuario` = user), since that is the language the project was written in.

## Endpoints

Base path: `/api`. Every endpoint requires the `Authorization: Bearer <token>` header
except the ones marked as public.

| Method | Endpoint | Public | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/Usuario/registrar` | Yes | Creates a user and returns their data plus a token |
| `POST` | `/api/Usuario/login` | Yes | Authenticates with email and password, returns the token |
| `GET` | `/api/Usuario` | No | Returns the user who owns the token |
| `GET` | `/api/Inmueble` | No | Lists every property |
| `GET` | `/api/Inmueble/{id}` | No | Returns one property; `404` if it does not exist |
| `POST` | `/api/Inmueble` | No | Creates a property and responds `201` with its location |
| `DELETE` | `/api/Inmueble/{id}` | No | Deletes a property; `404` if it does not exist |

Logging in and using the token:

```bash
# 1. Get a token with the demo user
TOKEN=$(curl -s -X POST http://localhost:5000/api/Usuario/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo@netkubernetes.local","password":"Demo_Password123$"}' \
  | python -c "import sys,json; print(json.load(sys.stdin)['token'])")

# 2. Query the properties with that token
curl -s http://localhost:5000/api/Inmueble -H "Authorization: Bearer $TOKEN"
```

Errors always come back in the same shape:

```json
{ "errores": { "mensaje": "No se encontro el inmueble por este id 99" } }
```

## Configuration

The repository holds **no credentials**. The app needs two values and will not start
without them: it fails immediately with a message naming what is missing.

| Key | Environment variable | What it is |
| --- | --- | --- |
| `ConnectionStrings:SQLServerConnection` | `ConnectionStrings__SQLServerConnection` | SQL Server connection string |
| `Jwt:Key` | `Jwt__Key` | JWT signing key, 32 characters minimum |

For local development `dotnet user-secrets` is the most convenient option, since it
stores the values outside the project directory:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:SQLServerConnection" \
  "Data Source=localhost;Initial Catalog=InmuebleDB;User Id=sa;Password=YourPassword123$;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
```

## Getting Started

Requirements: [.NET 7 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) and a
reachable SQL Server instance.

```bash
# Clone and restore
git clone https://github.com/Andersonporras/NetKubernetes.git
cd NetKubernetes
dotnet restore

# Configure the secrets (see the section above)
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "a-random-key-of-32-characters-or-more"
dotnet user-secrets set "ConnectionStrings:SQLServerConnection" "..."

# Run
dotnet run
```

The API listens on `http://localhost:5000` and Swagger UI is at
`http://localhost:5000/swagger`. On startup it applies the migrations and seeds two
properties plus a demo user:

- **Email**: `demo@netkubernetes.local`
- **Password**: `Demo_Password123$`

Migrations are applied automatically, but if you need to manage them by hand the
project ships `dotnet-ef` as a local tool:

```bash
dotnet tool restore
dotnet ef migrations add MigrationName
dotnet ef database update
```

## Docker

The `Dockerfile` uses a multi-stage build: it compiles and publishes with the SDK
image, then copies only the output into the much smaller ASP.NET runtime image. The
container listens on **port 5000** (`ASPNETCORE_URLS`), the same port `dotnet run`
uses and the same one the Kubernetes manifests target.

The fastest way to bring everything up — API and database — is Docker Compose:

```bash
cp .env.example .env     # edit MSSQL_SA_PASSWORD and JWT_KEY
docker compose up --build
```

Swagger lands on `http://localhost:5000/swagger`, and SQL Server data persists in a
volume, so you can stop and start again without losing anything.

To build and publish just the API image:

```bash
docker build -t your-user/net-kubernetes-api:v1.0 .
docker push your-user/net-kubernetes-api:v1.0

# Run it against an existing database
docker run -p 5000:5000 \
  -e ConnectionStrings__SQLServerConnection="Data Source=host.docker.internal;Initial Catalog=InmuebleDB;User Id=sa;Password=YourPassword123$;TrustServerCertificate=True" \
  -e Jwt__Key="a-random-key-of-32-characters-or-more" \
  your-user/net-kubernetes-api:v1.0
```

## The Kubernetes Manifests

> **I have not applied these manifests to a cluster.** They are the orchestration
> exercise from the course I followed: I wrote them, reviewed them, and fixed the
> errors I found by reading them, but the validation was structural, not a real
> deployment. The commands below are how it *should* be deployed, not a procedure I
> have run end to end.

[`KubernetesApp/`](KubernetesApp/) holds two variants on purpose: the bare `Pod`
files, which were the first step in understanding what a pod is, and the `Deployment`
files, which are what you actually use because they add replicas and automatic
replacement.

| File | Resource | Purpose |
| --- | --- | --- |
| `secrets.example.yml` | Secret | Template for passwords and the JWT key |
| `sqlserver-deploy.yml` | Deployment | SQL Server 2022, 1 replica |
| `sqlserver-service.yml` | Service | Exposes SQL Server inside the cluster (`ClusterIP`) |
| `app-deploy.yml` | Deployment | The API, 3 replicas |
| `app-service.yml` | Service | Exposes the API outside the cluster (`LoadBalancer`) |
| `sqlserver-pod.yml` | Pod | Single-pod version, for learning |
| `app-pod.yml` | Pod | Single-pod version, for learning |

How it would be deployed:

```bash
# 1. Create the Secret (the manifests read it via secretKeyRef)
kubectl create secret generic netkubernetes-secrets \
  --from-literal=sa-password='YourPassword123$' \
  --from-literal=sqlserver-connection='Data Source=mssql-service;Initial Catalog=InmuebleDB;User Id=sa;Password=YourPassword123$;TrustServerCertificate=True' \
  --from-literal=jwt-key="$(openssl rand -base64 48)"

# 2. Database first
kubectl apply -f KubernetesApp/sqlserver-deploy.yml
kubectl apply -f KubernetesApp/sqlserver-service.yml

# 3. Then the API
kubectl apply -f KubernetesApp/app-deploy.yml
kubectl apply -f KubernetesApp/app-service.yml

# 4. Check
kubectl get pods,svc
kubectl logs -l app.kubernetes.io/name=app --tail=50
```

The decisions I made while writing them, and why:

- **The API finds the database by Service name.** The connection string points at
  `mssql-service`, not an IP: the cluster's internal DNS resolves it, and it keeps
  working even when the SQL Server pod is recreated with a different IP.
- **Secrets are not in the manifests.** They are read with `secretKeyRef` from the
  `Secret`, which is created separately and never committed.
- **SQL Server is `ClusterIP`, not `LoadBalancer`.** The database is only consumed
  from inside the cluster; there is no reason to expose it to the network.
- **`ASPNETCORE_URLS` is explicit.** The ASP.NET image listens on port 80 by default
  while the `Service` targets 5000; without setting the variable the pod would start
  and the Service would route nothing.
- **Every container declares `requests` and `limits`**, so the scheduler knows where
  they fit and no container can starve the node.

## What I Learned

What I take away from building this:

- **Secure by default beats secure by decorator.** With a global `AuthorizeFilter`,
  forgetting `[Authorize]` no longer leaves an endpoint open. It is the kind of
  decision that costs the same to get right from the start.
- **The full JWT flow clicks once you wire it yourself.** It stopped being magic:
  login signs a token with a key, the client sends it in the header, and the
  middleware validates it with that same key. The part that took me longest was
  realizing the claim the generator writes and the one the session code reads have to
  match.
- **DTOs are not ceremony.** Returning the EF Core entity directly exposes internal
  columns and drags along relationships nobody asked for. Splitting input from output
  with AutoMapper made it clear what is public contract and what is a database detail.
- **Exception middleware saves repeating try/catch in every endpoint**, and it also
  guarantees errors always come out in the same shape.
- **Docker's multi-stage build makes sense once you see the size.** Compiling with the
  SDK image and running with the runtime one, copying only what was published, is the
  difference between a huge image and a reasonable one.
- **EF Core really does abstract the engine.** I tried MySQL with Pomelo and then
  settled on SQL Server, and the change was in the provider and the connection
  string, not in the model.
- **From Kubernetes I got the mental model, not the practice.** I understood the
  difference between a `Pod` (disposable), a `Deployment` (keeps it alive and
  replicated) and a `Service` (gives it a stable name), and why the database is
  referenced by Service name instead of by IP. But I know that from writing the
  manifests and reading the docs, not from operating a cluster; that is what I want to
  do next.

## Known Limitations and Next Steps

Things I know are missing, in case anyone reads the code:

- [ ] **The manifests are untested on a cluster.** The next step is standing up a
  local cluster (minikube or kind, neither of which asks for a credit card) and
  applying them for real to see what breaks.
- [ ] **Migrations run in every replica.** With 3 replicas, all three pods would try
  to migrate at once. The right answer is an `initContainer` or a `Job` that migrates
  exactly once before the API starts.
- [ ] **SQL Server has no persistent volume in Kubernetes.** If the pod is recreated,
  the data is gone. It needs a `PersistentVolumeClaim` (Docker Compose does have a volume).
- [ ] **No `livenessProbe` or `readinessProbe`**, because there is no health check
  endpoint to back them.
- [ ] **No tests.** Neither unit tests for the repositories nor integration tests for
  the endpoints.
- [ ] **Swagger is enabled in production too**, so the API can be explored from a
  container. In a real project it would sit behind authentication.
- [ ] **Dependencies need updating.** `AutoMapper 11.0.1` and
  `System.IdentityModel.Tokens.Jwt 6.22.0` have known advisories, and .NET 7 is out
  of support; the natural next step is migrating to .NET 8 LTS.
- [ ] **The CRUD is incomplete**: no `PUT` to update a property, and no pagination on
  the listing.

---

## Author

**Anderson Porras** — [GitHub](https://github.com/Andersonporras)

Frontend consuming this API:
[edificaciones-store-angular](https://github.com/Andersonporras/edificaciones-store-angular)
