using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Common;
using SchoolAdmission.Dtos;
using SchoolAdmission.Services;

namespace SchoolAdmission.Controllers;

[ApiController]
[Route("me")]
public class MeController : ControllerBase
{
    private readonly AdmissionService _svc;

    public MeController(AdmissionService svc) => _svc = svc;

    [HttpGet("holds")]
    public async Task<IActionResult> Holds(CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-Student-Id", out var values))
            throw new ApiException(ErrorCodes.MissingStudentId, "X-Student-Id header is required.", 400);
        var studentId = values.ToString();
        if (!AdmissionService.IsValidStudentId(studentId))
            throw new ApiException(ErrorCodes.InvalidStudentId, "Birth registration number must be 17 digits.", 400);

        var result = await _svc.GetMyHoldsAsync(studentId, ct);
        return Ok(result);
    }
}