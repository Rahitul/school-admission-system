# AI Usage

I used AI tools while doing this assignment, mostly for speeding up the boring
parts and for checking my own work. I wrote the final code myself and I
understand every part of it.

## Tools I used

- **Claude (Anthropic)** — I used it to draft the PRD and to think through the
  concurrency design (row lock vs. in-memory lock, the partial unique index,
  idempotent confirm). I also asked it to review my API contract against the
  brief.
- **OpenCode (Beam model)** — used for scaffolding boilerplate: empty .NET
  controller shells, the React component skeleton, Dockerfiles, docker-compose,
  and the first draft of the README/ASSUMPTIONS files.
- **GitHub Copilot (VS Code)** — small inline completions while typing
  (variable names, small LINQ expressions, CSS). I accepted only the ones I
  agreed with.

## Parts I did myself (no AI)

- The seat-counting logic and the `FOR UPDATE` transaction in
  `AdmissionService.CreateHoldAsync`.
- The expiry handling (lazy evaluation + the sweeper) and the decision to flip
  expired holds to EXPIRED inside the write transaction so re-application works.
- The frontend polling and countdown logic.
- All the debugging and the final testing.

## Where AI was wrong (and what I did about it)

1. **EF Core filtered unique index column name.** The first AI suggestion used a
   lowercase column name in the filter. PostgreSQL kept the column as
   PascalCase, so the index failed at runtime. I corrected the filter to use
   the quoted PascalCase name (`"Status"`).

2. **Expiry sweeper passed CancellationToken as a SQL parameter.** The generated
   code tried to bind the token as a parameter value, which threw. I fixed the
   call to pass it as the cancellation token argument of `ExecuteSqlRawAsync`.

3. **Concurrency test over-counted.** The first version of the test asserted 200
   created requests, which is impossible with 10 seats. I also hit "too many
   clients" from PostgreSQL because 200 simultaneous connections exceeded the
   default max_connections. I tuned Postgres (max_connections=300) and the
   Npgsql pool (Maximum Pool Size=200), and fixed the assertions to 10 created
   / 190 conflict.

4. **Student ID validation accepted Unicode digits.** AI used `char.IsDigit`,
   which accepts non-ASCII digits. I changed it to check `'0'`..`'9'` so it
   matches the 17-ASCII-digit rule and the frontend regex.

5. **Expired holds blocked re-application.** The partial unique index treated
   expired-but-not-yet-swept HELD rows as active. I added a lazy flip to
   EXPIRED inside the create-hold transaction before the insert.

6. **Confirm returned 500 after I added EnableRetryOnFailure.** The retrying
   execution strategy does not support user-initiated transactions, so ConfirmAsync
   crashed. I removed EnableRetryOnFailure and kept the startup retry loop.

## How I verified everything

- Built the backend and frontend locally (`dotnet build`, `npm run build`).
- Ran all 6 backend tests against real PostgreSQL, including the concurrency
  test with 200 parallel requests.
- Ran the 2 frontend tests with Vitest.
- Ran `docker compose up --build` on a clean state and walked through the whole
  flow in the browser (create class, apply, countdown, pay, cancel, expiry,
  refresh, browser reopen).
- Checked the database survived `docker compose down` then `up`.
- Read every AI-suggested change before keeping it. I rejected anything I could
  not explain in my own words.

## Note

I did not paste the assignment PDF into any AI tool, as the brief asks. I only
shared my own code and the PRD I wrote from the brief.