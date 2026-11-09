using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Common;
using SchoolAdmission.Dtos;
using SchoolAdmission.Services;

namespace SchoolAdmission.Controllers;

[ApiController]
[Route("holds")]
public class HoldsController : ControllerBase
{
    private readonly AdmissionService _svc;

    public HoldsController(AdmissionService svc) => _svc = svc;

    [HttpGet("{holdId}")]
    public async Task<IActionResult> Get(string holdId, CancellationToken ct)
    {
        var result = await _svc.GetHoldAsync(holdId, ct);
        return Ok(result);
    }

    [HttpPost("{holdId}/confirm")]
    public async Task<IActionResult> Confirm(string holdId, CancellationToken ct)
    {
        var studentId = GetStudentId();
        var result = await _svc.ConfirmAsync(holdId, studentId, ct);
        return Ok(result);
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> Cancel(string holdId, CancellationToken ct)
    {
        var studentId = GetStudentId();
        await _svc.CancelAsync(holdId, studentId, ct);
        return NoContent();
    }

    private string GetStudentId()
    {
        if (!Request.Headers.TryGetValue("X-Student-Id", out var values))
            throw new ApiException(ErrorCodes.MissingStudentId, "X-Student-Id header is required.", 400);
        var studentId = values.ToString();
        if (!AdmissionService.IsValidStudentId(studentId))
            throw new ApiException(ErrorCodes.InvalidStudentId, "Birth registration number must be 17 digits.", 400);
        return studentId;
    }
}