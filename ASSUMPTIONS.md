# Assumptions

This file lists the things the brief did not state clearly and what I decided
for each, with a short reason. The PRD (Section 21) gives the full list of gaps
(A-01 to A-26); I added a few more (A-27 onwards) for decisions I made while
building.

I tried to keep these decisions simple and close to what a Bangladeshi school
admission system actually needs.

---

## A-01 Deadline time zone
I assumed Bangladesh Standard Time (UTC+6).
Reason: the brief says "11 November 2026, 11:59:59 PM" but no time zone. Since
this is a local assignment, BST is the sensible reading. I also submitted early
to be safe.

## A-02 Identity and login
No login at all. The student is identified only by the X-Student-Id header.
Reason: the brief clearly says no login and no authentication (NG-2). Anyone who
knows the 17-digit number can act as that student, which is acceptable for
this scope.

## A-03 School admin access
No admin login or role. Admin screens are open to everyone.
Reason: NG-5 says no admin authentication or RBAC. Building auth would be out
of scope, so I left it open.

## A-04 Payment
Payment is fake. Clicking "Pay admission fee" counts as success.
Reason: NG-1 forbids a real payment gateway. The button just calls the confirm
endpoint directly, as the brief instructs.

## A-05 Multiple shifts
A student can hold one application in each class. Morning and Day shift are
separate classes, so both can be held at the same time.
Reason: the brief's rule is per class ("একটা class-এ"), not per student overall.

## A-06 Class name uniqueness
I reject duplicate class names (case-insensitive) with a 409 error.
Reason: two "Class 6 - Morning Shift" entries would confuse the admin and the
tests. Case-insensitive check stops "Class 6" and "class 6" being treated as
different.

## A-07 Cancel rules
Only a HELD hold can be cancelled.
Reason: the brief only mentions cancelling applications. A confirmed admission
is final and cannot be withdrawn through this API.

## A-08 Owner mismatch status code
I return 403 Forbidden when someone else's hold is touched.
Reason: the status-code table in the brief lists 403 for "hold belongs to
another student". Some people use 404 to hide existence, but 403 matches the
contract.

## A-09 Error body format
Errors look like {"error": {"code": "...", "message": "..."}}.
Reason: the brief does not define an error body (Section 12.1). This shape is
easy for the tests to check and easy for the UI to show.

## A-10 GET /holds/{id} needs no X-Student-Id
As written in the brief.
Reason: the brief specifies no header for this endpoint, so anyone with the
holdId can see its status. Fine for this scope (NG-2).

## A-11 Hold ID and class ID format
I use prefixed GUIDs like c_... and h_...
Reason: the brief says ID formats are examples only. Prefixed GUIDs are unique
and easy to read in logs.

## A-12 Scope boundaries
Single school, single academic year.
Reason: NG-7 excludes multi-school support unless it comes free. Keeping one
school keeps the model simple.

## A-13 Hold duration default and units
HOLD_DURATION_SECONDS, default 600, in seconds, must be positive.
Reason: matches the brief's default (600 s = 10 minutes). Bad or non-positive
values fall back to 600 so a hold never breaks.

## A-14 Expiry mechanism
Lazy evaluation plus a background sweeper every 5 seconds.
Reason: BR-2 says the seat must be free as soon as the time is over, with no
client call. Lazy evaluation guarantees correctness even if the sweeper is
not running; the sweeper keeps stored statuses tidy for reporting.

## A-15 Seat-count freshness in UI
Polling every few seconds (classes every 4 s, holds every 4-5 s).
Reason: the brief allows polling and does not require WebSockets. Simpler and
enough for a school.

## A-16 Student ID validation
Exactly 17 ASCII digits, leading zeros allowed.
Reason: the brief defines a 17-digit birth registration number. ASCII-only
avoids Unicode digit tricks and keeps backend and frontend in sync.

## A-17 Persistence of UI student ID
Stored in the browser's localStorage.
Reason: required by UI-4 so the student still sees their applications after a
refresh or reopening the browser, without any login system.

## A-18 Seed data
No seed data. The admin creates classes from the UI.
Reason: the brief says seed data is optional. The admin can create classes
from the UI with no manual database step, which satisfies NFR-4.

## A-19 Language of UI and messages
All UI text is in English.
Reason: the brief prefers Bengali but accepts English if clear. English keeps
the codebase and tests simple and avoids encoding problems.

## A-20 Hold for a class with zero seats
Rejected with 409 NO_SEATS.
Reason: BR-1 forbids held + confirmed exceeding totalSeats, so a full class
must reject new applications outright.

## A-21 Re-application after expiry
Allowed. An EXPIRED or CANCELLED hold does not block a new one.
Reason: BR-3 says only HELD or CONFIRMED block. Expired holds are flipped to
EXPIRED lazily so they stop occupying a seat and stop blocking re-application.

## A-22 Repeated confirm response
200 with CONFIRMED each time, no double count.
Reason: BR-5 requires idempotent confirmation. The conditional UPDATE affects
0 rows on repeat, so confirmedSeats increases only once.

## A-23 Concurrency approach
PostgreSQL row lock (SELECT ... FOR UPDATE) on the class row.
Reason: BR-1 must hold even when two backend processes share one database (the
bonus requirement). An in-memory lock would fail across processes, so the
protection must live in the database.

## A-24 Database migrations or schema creation
Schema is created automatically on startup via EnsureCreated.
Reason: NFR-4 needs a clean "docker compose up" with no manual steps. For this
schema EnsureCreated is enough; migrations would only matter if the schema
changed later.

## A-25 CORS
The backend allows the frontend origin (any origin in dev).
Reason: the browser calls the API directly from http://localhost:3000, so CORS
must allow it (A-25). The CORS_ORIGINS env var lets reviewers restrict it.

## A-26 Scale assumption
Designed for school-sized load (hundreds of concurrent applicants).
Reason: NFR-5 targets dozens to hundreds of concurrent users, not internet
scale. I tuned the connection pool (max_connections=300, pool size=200) so
the concurrency test passes.

## A-27 Student list (extra)
Added a "Students" tab and a GET /students endpoint listing each student ID
with their classes and statuses.
Reason: the user asked for an admin-style overview. It does not change the
PRD's 8-endpoint contract; it is just an extra view.

## A-28 Mobile responsiveness (extra)
Added responsive CSS for screens up to 640px wide.
Reason: NG-6 makes mobile optional, but a few media queries improve usability
on phones at no real cost.

## A-29 UI language (extra)
All UI text is in English.
Reason: the brief prefers Bengali but accepts English if clear. English keeps
the codebase and tests simple and avoids encoding problems.

## A-30 Root path redirect (extra)
"/" redirects to "/swagger".
Reason: the backend is an API with no landing page. Redirecting avoids a bare
404 and helps reviewers find the documentation.