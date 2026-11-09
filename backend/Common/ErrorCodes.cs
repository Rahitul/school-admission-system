namespace SchoolAdmission.Common;

public static class ErrorCodes
{
    public const string InvalidInput = "INVALID_INPUT";
    public const string MissingStudentId = "MISSING_STUDENT_ID";
    public const string InvalidStudentId = "INVALID_STUDENT_ID";
    public const string NotFound = "NOT_FOUND";
    public const string NotOwner = "NOT_OWNER";
    public const string NoSeats = "NO_SEATS";
    public const string AlreadyApplied = "ALREADY_APPLIED";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string HoldCancelled = "HOLD_CANCELLED";
    public const string InvalidState = "INVALID_STATE";
    public const string DuplicateClassName = "DUPLICATE_CLASS_NAME";
    public const string ServerError = "SERVER_ERROR";
}