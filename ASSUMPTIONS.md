# Assumptions

Each item below is a gap in the brief (PRD Section 21) with the decision taken
and the reason. A-01 ... A-26 are the exact gaps the PRD asks the candidate to
cover; A-27+ are extra decisions made during implementation.

---

## A-01 Deadline time zone
Decision: Bangladesh Standard Time (UTC+6).
Why: The brief states "11 November 2026, 11:59:59 PM" with no time zone.
Bangladesh Standard Time is the natural reading for a Bangladeshi assignment,
and submitting early removes any ambiguity.

## A-02 Identity and login
Decision: No login. Student identity = X-Student-Id header only.
Why: The brief explicitly says "no login, no authentication" (NG-2). Anyone
who knows the 17-digit number can act as that student; acceptable for this
scope, but a real system would need authentication.

## A-03 School admin access
Decision: No admin login or role; admin screens are open.
Why: NG-5 states no admin authentication or RBAC. Keeping it open matches the
brief and avoids building an auth system that is out of scope.

## A-04 Payment
Decision: Simulated; clicking "Pay admission fee" = success.
Why: NG-1 forbids a real payment gateway. The button calls confirm directly,
as the brief instructs.

## A-05 Multiple shifts
Decision: A student may hold one application per class; multiple shifts allowed.
Why: The brief's rule is per class ("একটা class-এ"), so Morning and Day shift
classes are independent pools and can both be held at once.

## A-06 Class name uniqueness
Decision: Reject duplicate class names (case-insensitive) with 409
DUPLICATE_CLASS_NAME.
Why: Prevents two identical "Class 6 - Morning Shift" entries that would
confuse admins and tests. Case-insensitive match avoids "Class 6" vs "class 6".

## A-07 Cancel rules
Decision: Only HELD holds can be cancelled.
Why: The brief lists cancel only for applications (holds). CONFIRMED admissions
are terminal and cannot be withdrawn through this API (409 INVALID_STATE).

## A-08 Owner mismatch status code
Decision: 403 Forbidden (NOT_OWNER).
Why: The brief's status-code table lists 403 for "hold belongs to another
student". Some designs hide existence with 404, but 403 matches the contract.

## A-09 Error body format
Decision: {"error":{"code","message"}}.
Why: The brief does not define an error body (Section 12.1). This shape is
machine-readable for tests and human-readable for the UI.

## A-10 GET /holds/{id} needs no X-Student-Id
Decision: As written in the brief.
Why: The brief specifies no header for this endpoint, so anyone with the holdId
can view its status. Acceptable for this scope (NG-2).

## A-11 Hold ID and class ID format
Decision: Prefixed GUIDs (c_..., h_...).
Why: The brief says ID formats are examples only. Prefixed GUIDs are unique,
opaque to clients, and easy to read in logs.

## A-12 Scope boundaries
Decision: Single school, single academic year.
Why: NG-7 excludes multi-school support unless free. Keeping one school keeps
the model simple.

## A-13 Hold duration default and units
Decision: HOLD_DURATION_SECONDS, default 600, positive integer seconds.
Why: Matches the brief's default (600 s = 10 min). Invalid or non-positive
values fall back to 600 to avoid a broken hold.

## A-14 Expiry mechanism
Decision: Lazy evaluation plus a background sweeper every 5 s.
Why: BR-2 requires the seat to be free as soon as expiresAt passes without any
client call. Lazy evaluation guarantees correctness even without the sweeper;
the sweeper keeps stored statuses accurate for reporting.

## A-15 Seat-count freshness in UI
Decision: Polling every few seconds (classes 4 s, holds 4-5 s).
Why: The brief explicitly allows polling and does not require WebSockets.
Simpler and sufficient for school-scale load.

## A-16 Student ID validation
Decision: Exactly 17 ASCII digits, leading zeros allowed.
Why: The brief defines a 17-digit birth registration number. ASCII-only avoids
Unicode digit tricks and keeps backend/frontend validation consistent.

## A-17 Persistence of UI student ID
Decision: Stored in browser localStorage.
Why: Required by UI-4 so the student still sees their applications after
refresh or browser restart, without a login system.

## A-18 Seed data
Decision: No seed data; admin creates classes via the UI.
Why: The brief says seed data is optional (A-18). Admin can create classes
from the UI with no manual DB step, satisfying NFR-4.

## A-19 Language of UI and messages
Decision: English throughout (originally Bengali/English mix).
Why: The brief prefers Bengali but accepts English if clear. English keeps
the codebase and tests simple and avoids encoding issues.

## A-20 Hold for a class with zero seats
Decision: Rejected with 409 NO_SEATS.
Why: BR-1 forbids held+confirmed exceeding totalSeats; a full class must
reject new holds explicitly.

## A-21 Re-application after expiry
Decision: Allowed; an EXPIRED or CANCELLED hold does not block a new hold.
Why: BR-3 states only HELD/CONFIRMED block. Expired holds are flipped to
EXPIRED lazily so they stop occupying a seat and stop blocking re-application.

## A-22 Repeated confirm response
Decision: 200 with CONFIRMED each time, no double count.
Why: BR-5 requires idempotent confirmation. The conditional UPDATE affects 0
rows on repeat, so confirmedSeats increases only once.

## A-23 Concurrency approach
Decision: PostgreSQL row lock (SELECT ... FOR UPDATE) on the class row.
Why: BR-1 must hold even across multiple backend processes sharing one DB
(bonus requirement). An in-memory lock (Option D) would fail across processes,
so protection must live in the database.

## A-24 Database migrations or schema creation
Decision: Schema created automatically on startup via EnsureCreated.
Why: NFR-4 requires a clean "docker compose up" with no manual steps. For this
schema, EnsureCreated is sufficient; migrations would be needed only if the
schema evolved after release.

## A-25 CORS
Decision: Backend allows the frontend origin (and any origin in dev).
Why: The browser calls the API directly from http://localhost:3000, so CORS
must allow it (A-25). CORS_ORIGINS env var lets reviewers restrict it.

## A-26 Scale assumption
Decision: Designed for school-sized load (hundreds of concurrent applicants).
Why: NFR-5 targets dozens to hundreds of concurrent users, not internet scale.
Connection pool and DB limits were tuned (max_connections=300, pool size=200)
to satisfy the concurrency test.

## A-27 Student list (extra)
Decision: Added a "Students" tab and GET /students endpoint listing each
student ID with their classes and statuses.
Why: Requested by the user as an admin-style overview. It does not change the
PRD's 8-endpoint contract; it is an additive convenience view.

## A-28 Mobile responsiveness (extra)
Decision: Added responsive CSS at <=640px.
Why: NG-6 makes mobile optional, but a few media queries improve usability on
phones at no cost.

## A-29 UI language (extra)
Decision: All UI text is in English.
Why: The brief prefers Bengali but accepts English if clear. English keeps the
codebase and automated tests simple and avoids encoding issues.

## A-30 Root path redirect (extra)
Decision: "/" redirects to "/swagger".
Why: The backend is an API with no landing page; redirecting avoids a bare 404
and helps reviewers find the documentation.