using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Common;
using SchoolAdmission.Data;
using SchoolAdmission.Data.Entities;
using SchoolAdmission.Dtos;

namespace SchoolAdmission.Services;

public class AdmissionService
{
    private readonly AppDbContext _db;
    private readonly int _holdDurationSeconds;
    private readonly ILogger<AdmissionService> _logger;

    public AdmissionService(AppDbContext db, IConfiguration config, ILogger<AdmissionService> logger)
    {
        _db = db;
        _logger = logger;
        var raw = Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS")
                  ?? config["HOLD_DURATION_SECONDS"]
                  ?? "600";
        if (!int.TryParse(raw, out var seconds) || seconds <= 0)
        {
            seconds = 600;
        }
        _holdDurationSeconds = seconds;
    }

    public int HoldDurationSeconds => _holdDurationSeconds;

    private static string NewClassId() => "c_" + Guid.NewGuid().ToString("N");
    private static string NewHoldId() => "h_" + Guid.NewGuid().ToString("N");

    private static bool IsHeldActive(HoldEntity h, DateTime now) =>
        h.Status == "HELD" && h.ExpiresAt > now;

    public async Task<ClassResponse> CreateClassAsync(string name, int totalSeats, CancellationToken ct)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ApiException(ErrorCodes.InvalidInput, "Class name is required.", 400);
        if (totalSeats < 1)
            throw new ApiException(ErrorCodes.InvalidInput, "Total seats must be at least 1.", 400);

        var exists = await _db.Classes.AnyAsync(c => c.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new ApiException(ErrorCodes.DuplicateClassName, "A class with this name already exists.", 409);

        var cls = new ClassEntity
        {
            Id = NewClassId(),
            Name = name,
            TotalSeats = totalSeats,
            CreatedAt = DateTime.UtcNow
        };
        _db.Classes.Add(cls);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Class created {ClassId} {Name}", cls.Id, cls.Name);
        return new ClassResponse(cls.Id, cls.Name, cls.TotalSeats);
    }

    public async Task<List<ClassSummaryResponse>> GetClassesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var classes = await _db.Classes.AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync(ct);
        var holds = await _db.Holds.AsNoTracking().ToListAsync(ct);

        var result = new List<ClassSummaryResponse>();
        foreach (var c in classes)
        {
            var classHolds = holds.Where(h => h.ClassId == c.Id).ToList();
            var held = classHolds.Count(h => IsHeldActive(h, now));
            var confirmed = classHolds.Count(h => h.Status == "CONFIRMED");
            var available = c.TotalSeats - held - confirmed;
            if (available < 0) available = 0;
            result.Add(new ClassSummaryResponse(c.Id, c.Name, c.TotalSeats, held, confirmed, available));
        }
        return result;
    }

    public async Task<ClassSummaryResponse> GetClassAsync(string classId, CancellationToken ct)
    {
        var cls = await _db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId, ct);
        if (cls == null)
            throw new ApiException(ErrorCodes.NotFound, "Class not found.", 404);

        var now = DateTime.UtcNow;
        var holds = await _db.Holds.AsNoTracking().Where(h => h.ClassId == classId).ToListAsync(ct);
        var held = holds.Count(h => IsHeldActive(h, now));
        var confirmed = holds.Count(h => h.Status == "CONFIRMED");
        var available = cls.TotalSeats - held - confirmed;
        if (available < 0) available = 0;
        return new ClassSummaryResponse(cls.Id, cls.Name, cls.TotalSeats, held, confirmed, available);
    }

    public async Task<HoldResponse> CreateHoldAsync(string classId, string studentId, CancellationToken ct)
    {
        if (!IsValidStudentId(studentId))
            throw new ApiException(ErrorCodes.InvalidStudentId, "Birth registration number must be 17 digits.", 400);

        var conn = (Npgsql.NpgsqlConnection)_db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            int totalSeats;
            await using (var lockCmd = conn.CreateCommand())
            {
                lockCmd.Transaction = tx;
                lockCmd.CommandText = "SELECT \"TotalSeats\" FROM classes WHERE \"Id\" = @id FOR UPDATE";
                var p = lockCmd.CreateParameter();
                p.ParameterName = "@id";
                p.Value = classId;
                lockCmd.Parameters.Add(p);
                var result = await lockCmd.ExecuteScalarAsync(ct);
                if (result == null || result == DBNull.Value)
                    throw new ApiException(ErrorCodes.NotFound, "Class not found.", 404);
                totalSeats = Convert.ToInt32(result);
            }

            var now = DateTime.UtcNow;

            // Lazy expiry: flip expired HELD holds to EXPIRED so they stop
            // occupying a seat and no longer block re-application (BR-3).
            await using (var exp = conn.CreateCommand())
            {
                exp.Transaction = tx;
                exp.CommandText = "UPDATE holds SET \"Status\" = 'EXPIRED' WHERE \"ClassId\" = @cid AND \"Status\" = 'HELD' AND \"ExpiresAt\" <= @now";
                var pe1 = exp.CreateParameter(); pe1.ParameterName = "@cid"; pe1.Value = classId; exp.Parameters.Add(pe1);
                var pe2 = exp.CreateParameter(); pe2.ParameterName = "@now"; pe2.Value = now; exp.Parameters.Add(pe2);
                await exp.ExecuteNonQueryAsync(ct);
            }

            long heldCount;
            await using (var c1 = conn.CreateCommand())
            {
                c1.Transaction = tx;
                c1.CommandText = "SELECT COUNT(*) FROM holds WHERE \"ClassId\" = @cid AND \"Status\" = 'HELD' AND \"ExpiresAt\" > @now";
                var p1 = c1.CreateParameter(); p1.ParameterName = "@cid"; p1.Value = classId; c1.Parameters.Add(p1);
                var p2 = c1.CreateParameter(); p2.ParameterName = "@now"; p2.Value = now; c1.Parameters.Add(p2);
                heldCount = (long)(await c1.ExecuteScalarAsync(ct) ?? 0L);
            }

            long confirmedCount;
            await using (var c2 = conn.CreateCommand())
            {
                c2.Transaction = tx;
                c2.CommandText = "SELECT COUNT(*) FROM holds WHERE \"ClassId\" = @cid AND \"Status\" = 'CONFIRMED'";
                var p = c2.CreateParameter(); p.ParameterName = "@cid"; p.Value = classId; c2.Parameters.Add(p);
                confirmedCount = (long)(await c2.ExecuteScalarAsync(ct) ?? 0L);
            }

            if (heldCount + confirmedCount >= totalSeats)
                throw new ApiException(ErrorCodes.NoSeats, "No seats are left in this class.", 409);

            long heldActive;
            await using (var c3a = conn.CreateCommand())
            {
                c3a.Transaction = tx;
                c3a.CommandText = "SELECT COUNT(*) FROM holds WHERE \"ClassId\" = @cid AND \"StudentId\" = @sid AND \"Status\" = 'HELD' AND \"ExpiresAt\" > @now";
                var p1 = c3a.CreateParameter(); p1.ParameterName = "@cid"; p1.Value = classId; c3a.Parameters.Add(p1);
                var p2 = c3a.CreateParameter(); p2.ParameterName = "@sid"; p2.Value = studentId; c3a.Parameters.Add(p2);
                var p3 = c3a.CreateParameter(); p3.ParameterName = "@now"; p3.Value = now; c3a.Parameters.Add(p3);
                heldActive = (long)(await c3a.ExecuteScalarAsync(ct) ?? 0L);
            }

            long confirmedActive;
            await using (var c3b = conn.CreateCommand())
            {
                c3b.Transaction = tx;
                c3b.CommandText = "SELECT COUNT(*) FROM holds WHERE \"ClassId\" = @cid AND \"StudentId\" = @sid AND \"Status\" = 'CONFIRMED'";
                var p1 = c3b.CreateParameter(); p1.ParameterName = "@cid"; p1.Value = classId; c3b.Parameters.Add(p1);
                var p2 = c3b.CreateParameter(); p2.ParameterName = "@sid"; p2.Value = studentId; c3b.Parameters.Add(p2);
                confirmedActive = (long)(await c3b.ExecuteScalarAsync(ct) ?? 0L);
            }

            if (heldActive > 0)
                throw new ApiException(ErrorCodes.AlreadyApplied, "You are on hold in this class.", 409);
            if (confirmedActive > 0)
                throw new ApiException(ErrorCodes.AlreadyApplied, "You have already applied to this class.", 409);

            var holdId = NewHoldId();
            var expiresAt = now.AddSeconds(_holdDurationSeconds);

            await using (var ins = conn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = "INSERT INTO holds (\"Id\", \"ClassId\", \"StudentId\", \"Status\", \"CreatedAt\", \"ExpiresAt\") VALUES (@id, @cid, @sid, 'HELD', @now, @exp)";
                var p1 = ins.CreateParameter(); p1.ParameterName = "@id"; p1.Value = holdId; ins.Parameters.Add(p1);
                var p2 = ins.CreateParameter(); p2.ParameterName = "@cid"; p2.Value = classId; ins.Parameters.Add(p2);
                var p3 = ins.CreateParameter(); p3.ParameterName = "@sid"; p3.Value = studentId; ins.Parameters.Add(p3);
                var p4 = ins.CreateParameter(); p4.ParameterName = "@now"; p4.Value = now; ins.Parameters.Add(p4);
                var p5 = ins.CreateParameter(); p5.ParameterName = "@exp"; p5.Value = expiresAt; ins.Parameters.Add(p5);
                try
                {
                    await ins.ExecuteNonQueryAsync(ct);
                }
                catch (Npgsql.PostgresException pg) when (pg.SqlState == "23505")
                {
                    throw new ApiException(ErrorCodes.AlreadyApplied, "You have already applied to this class.", 409);
                }
            }

            await tx.CommitAsync(ct);
            _logger.LogInformation("Hold created {HoldId} class {ClassId} student {StudentId}", holdId, classId, studentId);
            return new HoldResponse(holdId, classId, studentId, "HELD", expiresAt);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<HoldStatusResponse> GetHoldAsync(string holdId, CancellationToken ct)
    {
        var hold = await _db.Holds.AsNoTracking().FirstOrDefaultAsync(h => h.Id == holdId, ct);
        if (hold == null)
            throw new ApiException(ErrorCodes.NotFound, "Application not found.", 404);

        var now = DateTime.UtcNow;
        var status = hold.Status;
        if (status == "HELD" && hold.ExpiresAt <= now)
            status = "EXPIRED";

        return new HoldStatusResponse(hold.Id, hold.ClassId, status, hold.ExpiresAt);
    }

    public async Task<ConfirmResponse> ConfirmAsync(string holdId, string studentId, CancellationToken ct)
    {
        if (!IsValidStudentId(studentId))
            throw new ApiException(ErrorCodes.InvalidStudentId, "Birth registration number must be 17 digits.", 400);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var hold = await _db.Holds.FirstOrDefaultAsync(h => h.Id == holdId, ct);
            if (hold == null)
                throw new ApiException(ErrorCodes.NotFound, "Application not found.", 404);

            if (hold.StudentId != studentId)
                throw new ApiException(ErrorCodes.NotOwner, "This application does not belong to you.", 403);

            var now = DateTime.UtcNow;

            // idempotent
            if (hold.Status == "CONFIRMED")
            {
                await tx.CommitAsync(ct);
                return new ConfirmResponse(hold.Id, "CONFIRMED");
            }

            if (hold.Status == "CANCELLED")
                throw new ApiException(ErrorCodes.HoldCancelled, "This application was cancelled.", 409);

            var expired = hold.Status == "EXPIRED" || (hold.Status == "HELD" && hold.ExpiresAt <= now);
            if (expired)
            {
                if (hold.Status == "HELD")
                {
                    hold.Status = "EXPIRED";
                    await _db.SaveChangesAsync(ct);
                }
                throw new ApiException(ErrorCodes.HoldExpired, "Your application time has expired. Please apply again.", 409);
            }

            if (hold.Status != "HELD")
                throw new ApiException(ErrorCodes.InvalidState, "Operation not allowed in current status.", 409);

            var affected = await _db.Database.ExecuteSqlRawAsync(
                "UPDATE holds SET \"Status\" = 'CONFIRMED', \"ConfirmedAt\" = {0} WHERE \"Id\" = {1} AND \"Status\" = 'HELD' AND \"ExpiresAt\" > {2}",
                new object[] { now, holdId, now }, ct);

            if (affected == 0)
            {
                await _db.Entry(hold).ReloadAsync(ct);
                if (hold.Status == "CONFIRMED")
                {
                    await tx.CommitAsync(ct);
                    return new ConfirmResponse(hold.Id, "CONFIRMED");
                }
                if (hold.ExpiresAt <= DateTime.UtcNow)
                    throw new ApiException(ErrorCodes.HoldExpired, "Your application time has expired. Please apply again.", 409);
                throw new ApiException(ErrorCodes.InvalidState, "Operation not allowed in current status.", 409);
            }

            hold.Status = "CONFIRMED";
            hold.ConfirmedAt = now;
            await tx.CommitAsync(ct);
            _logger.LogInformation("Hold confirmed {HoldId}", holdId);
            return new ConfirmResponse(hold.Id, "CONFIRMED");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task CancelAsync(string holdId, string studentId, CancellationToken ct)
    {
        if (!IsValidStudentId(studentId))
            throw new ApiException(ErrorCodes.InvalidStudentId, "Birth registration number must be 17 digits.", 400);

        var hold = await _db.Holds.FirstOrDefaultAsync(h => h.Id == holdId, ct);
        if (hold == null)
            throw new ApiException(ErrorCodes.NotFound, "Application not found.", 404);

        if (hold.StudentId != studentId)
            throw new ApiException(ErrorCodes.NotOwner, "This application does not belong to you.", 403);

        var now = DateTime.UtcNow;
        var status = hold.Status;
        if (status == "HELD" && hold.ExpiresAt <= now)
            status = "EXPIRED";

        if (status != "HELD")
            throw new ApiException(ErrorCodes.InvalidState, "Operation not allowed in current status.", 409);

        hold.Status = "CANCELLED";
        hold.CancelledAt = now;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Hold cancelled {HoldId}", holdId);
    }

    public async Task<List<StudentSummaryResponse>> GetStudentsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var holds = await _db.Holds.AsNoTracking().OrderByDescending(h => h.CreatedAt).ToListAsync(ct);
        var classIds = holds.Select(h => h.ClassId).Distinct().ToList();
        var classes = await _db.Classes.AsNoTracking()
            .Where(c => classIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var grouped = holds
            .GroupBy(h => h.StudentId)
            .Select(g => new StudentSummaryResponse(
                g.Key,
                g.Select(h =>
                {
                    var status = h.Status;
                    if (status == "HELD" && h.ExpiresAt <= now) status = "EXPIRED";
                    var className = classes.TryGetValue(h.ClassId, out var n) ? n : h.ClassId;
                    return new StudentHoldSummary(h.Id, h.ClassId, className, status, h.ExpiresAt);
                }).ToList()))
            .OrderBy(s => s.StudentId)
            .ToList();

        return grouped;
    }

    public async Task<List<MyHoldResponse>> GetMyHoldsAsync(string studentId, CancellationToken ct)
    {
        if (!IsValidStudentId(studentId))
            throw new ApiException(ErrorCodes.InvalidStudentId, "Birth registration number must be 17 digits.", 400);

        var now = DateTime.UtcNow;
        var holds = await _db.Holds.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        var classIds = holds.Select(h => h.ClassId).Distinct().ToList();
        var classes = await _db.Classes.AsNoTracking()
            .Where(c => classIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var result = new List<MyHoldResponse>();
        foreach (var h in holds)
        {
            var status = h.Status;
            if (status == "HELD" && h.ExpiresAt <= now)
                status = "EXPIRED";
            var className = classes.TryGetValue(h.ClassId, out var n) ? n : h.ClassId;
            result.Add(new MyHoldResponse(h.Id, h.ClassId, className, status, h.ExpiresAt));
        }
        return result;
    }

    public static bool IsValidStudentId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.Length == 17
               && value.All(c => c >= '0' && c <= '9');
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("23505", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("unique", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
    }
}