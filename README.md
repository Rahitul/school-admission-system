# School Admission System

Backend: .NET 8 Web API + PostgreSQL. Frontend: React 18 + Vite.

## Prerequisites
- Docker (and Docker Compose) only.

## Run
```bash
docker compose up --build
```
(`--build` is only needed the first time or after code changes; afterwards `docker compose up` is enough.)

## Run frontend locally
```bash
cd frontend
npm install
npm run dev
```
Then open http://localhost:5173.

- Frontend: http://localhost:3000
- Backend: http://localhost:8080
- Swagger: http://localhost:8080/swagger
- Health: http://localhost:8080/health

Stop without losing data:
```bash
docker compose down
```
Data persists in the named volume `pgdata`. Do NOT use `down -v`.

## Environment variables
Backend:
- `ConnectionStrings__DefaultConnection` / `DATABASE_URL`: PostgreSQL connection string.
- `HOLD_DURATION_SECONDS`: hold lifetime in seconds (default 20).
- `CORS_ORIGINS`: comma-separated allowed origins (default `http://localhost:3000,http://localhost:5173`; use `*` for any).

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
(Frontend tests are a bonus; included as a basic smoke test.)

## Frontend configuration in Docker
The frontend uses Vite, so VITE_API_BASE_URL is replaced at build time. This project
uses Option 2 from the PRD (Section 17.3): the value is passed as a Docker build
argument in docker-compose.yml. If you change it, rebuild with:
  docker compose up --build

## Frontend workflow
1. Open http://localhost:3000. You land on the **Classes** tab.
2. Type your 17-digit birth registration number in the top box and click **Save**
   (stored in localStorage; survives refresh / browser reopen).
3. Browse classes: each card shows name, total, held, confirmed, available seats.
   Seat counts refresh every ~4 seconds.
4. Click **Apply** on a class to create a hold (HELD, expires in HOLD_DURATION_SECONDS,
   default 20 sec). Disabled if no seats left or you already applied to that class.
5. The **Application Detail** modal opens: status, expiry countdown, and two buttons:
   - **Pay admission fee** (only when HELD & not expired) -> confirms the admission.
   - **Cancel** (only when HELD) -> cancels and releases the seat.
   When the countdown hits zero, status becomes EXPIRED without reload and Pay disables.
6. **My Applications** tab lists all your applications (HELD, CONFIRMED, EXPIRED,
   CANCELLED) with class name, status, and expiry; click **View** for details.
7. **Create Class** tab (admin): enter name + total seats (1-10000) and submit.
8. **Students** tab (admin view): lists each student ID with their classes and statuses.
9. Errors (invalid ID, no seats, expired, server down) show as readable messages;
   a loading indicator shows during API calls.

## Project structure
```
backend/        .NET 8 Web API, EF Core, PostgreSQL, tests
frontend/       React + Vite SPA
docker-compose.yml
README.md
ASSUMPTIONS.md
AI_USAGE.md
```