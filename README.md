# AsyncJobScheduler

Training template for an asynchronous job scheduler built with .NET 10 and a minimal web UI based on React, Vite, and Tailwind CSS.

## Features

- create jobs via `POST /api/jobs`
- list all jobs via `GET /api/jobs`
- fetch a single job via `GET /api/jobs/{id}`
- wait for a job to finish via `GET /api/jobs/{id}/wait`
- cancel non-terminal jobs via `DELETE /api/jobs/{id}`
- run the full stack locally or with Docker Compose

## Tech Stack

- Backend: ASP.NET Core Minimal API, .NET 10
- Frontend: React 19, TypeScript, Vite 6, Tailwind CSS 4
- Containers: Docker, Docker Compose, Nginx

## Repository Structure

- `src/` - solution and backend projects
- `src-front/` - frontend application
- `api/openapi.yaml` - OpenAPI contract
- `tests/` - unit tests

## Run with Docker Compose

From the repository root:

```bash
docker compose up --build
```

After startup the services are available at:

- UI: [http://localhost:4173](http://localhost:4173)
- API: [http://localhost:5121](http://localhost:5121)

Stop the containers with:

```bash
docker compose down
```

## Local Run

### 1. Requirements

Install the following tools first:

- .NET SDK 10.0
- Node.js 22+
- npm 10+

### 2. Run the backend

```bash
cd src
dotnet restore AsyncJobScheduler.sln
dotnet run --project AsyncJobScheduler.API
```

By default the API starts on [http://localhost:5121](http://localhost:5121).

### 3. Run the frontend

Open another terminal:

```bash
cd src-front
npm install
npm run dev
```

The frontend will be available at [http://localhost:5173](http://localhost:5173).

In development mode Vite proxies `/api` requests to the backend, so no extra CORS setup is required.

## Useful Commands

Run backend tests:

```bash
dotnet test tests/AsyncJobScheduler.Infrastructure.UnitTests/AsyncJobScheduler.Infrastructure.UnitTests.csproj
```

Build the frontend:

```bash
cd src-front
npm run build
```

## API and Sample Requests

- OpenAPI: [api/openapi.yaml](api/openapi.yaml)
- HTTP examples: [api/requests/jobs.http](api/requests/jobs.http)

## Production Notes

- the frontend container is served by Nginx
- Nginx proxies `/api` requests to the backend container
- the proxy timeout is increased for the long-polling `GET /api/jobs/{id}/wait` endpoint
