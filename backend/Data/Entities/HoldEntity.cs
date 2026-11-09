namespace SchoolAdmission.Data.Entities;

public class HoldEntity
{
    public string Id { get; set; } = string.Empty;
    public string ClassId { get; set; } = string.Empty;
    public ClassEntity? Class { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string Status { get; set; } = "HELD";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}