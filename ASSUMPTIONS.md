## 01 Identity and login
No login at all. The student is identified only by the X-Student-Id header.
Reason: the brief clearly says no login and no authentication. Anyone who knows the 17-digit number can act as that student, 
which is acceptable for this scope.

## 02 School admin access
No admin login or role. Admin screens are open to everyone.
Reason: In the pdf says no admin authentication. Building auth would be out of scope, so I left it open.

## 03 Payment
Payment is fake. Clicking "Pay admission fee" counts as success.
Reason: In pdf forbids a real payment gateway. The button just calls the confirm endpoint directly, as the brief instructs.

## 04 Multiple shifts
A student can hold one application in each class. Morning and Day shift are eparate classes, so both can be held at the same time.
Reason: the brief's rule is per class, not per student overall.

## 05 Class name uniqueness
I reject duplicate class names (case-insensitive) with a 409 error.
Reason: two "Class 6 - Morning Shift" entries would confuse the admin and the tests. Case-insensitive check stops "Class 6" and "class 6" being treated as different.

## 06 Cancel rules
Only a HELD hold can be cancelled.
Reason: the brief only mentions cancelling applications. A confirmed admission is final and cannot be withdrawn through this API.

## 07 Error body format
Errors look like {"error": {"code": "...", "message": "..."}}.
Reason: the brief does not define an error body. This shape is easy for the tests to check and easy for the UI to show.

## 08 GET /holds/{id} needs no X-Student-Id
As written in the brief.
Reason: the brief specifies no header for this endpoint, so anyone with the holdId can see its status.

## 09 Hold ID and class ID format
I use prefixed GUIDs like c_... and h_...
Reason: the brief says ID formats are examples only. Prefixed GUIDs are unique and easy to read in logs.

## 10 Hold duration default and units
HOLD_DURATION_SECONDS, default 600, in seconds, must be positive.
Reason: matches the brief's default (600 s = 10 minutes). Bad or non-positive values fall back to 600 so a hold never breaks.

## 11 Expiry mechanism
Lazy evaluation plus a background sweeper every 5 seconds.
Reason: In the doc says the seat must be free as soon as the time is over, with no client call. Lazy evaluation guarantees 
correctness even if the sweeper is not running; the sweeper keeps stored statuses tidy for reporting.

## 12 Seat-count freshness in UI
Polling every few seconds (classes every 4 s, holds every 4-5 s).
Reason: the brief allows polling and does not require WebSockets.

## 13 Student ID validation
Exactly 17 ASCII digits, leading zeros allowed.
Reason: the brief defines a 17-digit birth registration number. ASCII-only avoids Unicode digit tricks and keeps backend and frontend in sync.

## 14 Persistence of UI student ID
Stored in the browser's localStorage.
Reason: required by UI-4 so the student still sees their applications after a refresh or reopening the browser, without any login system.

## 15 Seed data
No seed data. The admin creates classes from the UI.
Reason: the brief says seed data is optional. The admin can create classes from the UI with no manual database step.

## 16 Hold for a class with zero seats
Rejected with 409 NO_SEATS.
Reason: forbids held + confirmed exceeding totalSeats, so a full class must reject new applications outright.

## 17 Re-application after expiry
Allowed. An EXPIRED or CANCELLED hold does not block a new one.
Reason: says only HELD or CONFIRMED block. Expired holds are flipped to EXPIRED lazily so they stop occupying a seat and stop blocking re-application.

## 18 Repeated confirm response
200 with CONFIRMED each time, no double count.
Reason: requires idempotent confirmation. The conditional UPDATE affects 0 rows on repeat, so confirmedSeats increases only once.

## 19 Database migrations or schema creation
Schema is created automatically on startup via EnsureCreated.
Reason: needs a clean "docker compose up" with no manual steps. For this schema EnsureCreated is enough; migrations would only matter if the schema
changed later.

## 20 Scale assumption
Designed for school-sized load (hundreds of concurrent applicants).
Reason: NFR-5 targets dozens to hundreds of concurrent users, not internet scale. I tuned the connection pool (max_connections=300, pool size=200) so the concurrency test passes.

## 21 Student list (extra)
Added a "Students" tab and a GET /students endpoint listing each student ID with their classes and statuses.
Reason: the user asked for an admin-style overview. It does not change the PRD's 8-endpoint contract; it is just an extra view.

## 22 Mobile responsiveness (extra)
Added responsive CSS for screens up to 640px wide.
Reason: makes mobile optional, but a few media queries improve usability on phones at no real cost.

## 23 Root path redirect (extra)
"/" redirects to "/swagger".
Reason: the backend is an API with no landing page. Redirecting avoids a bare
404 and helps reviewers find the documentation.