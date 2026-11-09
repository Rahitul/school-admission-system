namespace SchoolAdmission.Common;

public class ErrorBody
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public ErrorBody Error { get; set; } = new();
}