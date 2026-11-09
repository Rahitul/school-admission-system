# School Admission System

Backend: .NET 8 Web API + PostgreSQL. Frontend: React 18 + Vite.

## Prerequisites
- Docker (and Docker Compose) only.

## Run
```bash
docker compose up --build
```
- Frontend: http://localhost:3000
- Backend: http://localhost:8080
- Swagger: http://localhost:8080/swagger

Stop without losing data:
```bash
docker compose down
```
Data persists in the named volume `pgdata`. Do NOT use `down -v`.

## Environment variables
Backend:
- `ConnectionStrings__DefaultConnection` / `DATABASE_URL`: PostgreSQL connection string.
- `HOLD_DURATION_SECONDS`: hold lifetime in seconds (default 600).

Frontend:
- `VITE_API_BASE_URL`: backend URL (default http://localhost:8080).

Database container:
- `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`.

## Tests
Backend:
```bash
cd backend/backend.Tests
dotnet test
```
Requires a running PostgreSQL (set `TEST_DATABASE_URL`, default localhost:5432). Or via Docker:
```bash
docker compose --profile test run --rm backend-tests
```

Frontend:
```bash
cd frontend && npm test
```
(Frontend tests are a bonus; not included in this version.)

## Project structure
```
backend/        .NET 8 Web API, EF Core, PostgreSQL
frontend/       React + Vite SPA
docker-compose.yml
README.md
ASSUMPTIONS.md
AI_USAGE.md
```