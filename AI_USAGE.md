# AI Usage

Tool: OpenCode agent (Beam model) used to scaffold and implement the project.

Parts done with AI:
- Backend and frontend boilerplate and implementation.
- Concurrency design (row lock) and tests.
- Docker files and compose.
- Documentation drafting.

Mistakes caught:
- Initial EF Core filtered unique index used lowercase column name; corrected to quoted PascalCase.
- Expiry sweeper passed CancellationToken as SQL parameter; corrected overload.
- Concurrency test initially over-counted due to short hold duration; adjusted test duration and assertions.

Verification:
- Built backend and frontend locally.
- Ran 6 backend integration tests against PostgreSQL, including concurrency test.