using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Common;
using SchoolAdmission.Dtos;
using SchoolAdmission.Services;

namespace SchoolAdmission.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly AdmissionService _svc;

    public ClassesController(AdmissionService svc) => _svc = svc;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest? request, CancellationToken ct)
    {
        if (request == null)
            throw new ApiException(ErrorCodes.InvalidInput, "Request body is required.", 400);

        var name = request.Name;
        if (string.IsNullOrWhiteSpace(name))
            throw new ApiException(ErrorCodes.InvalidInput, "Class name is required.", 400);

        if (request.TotalSeats is null || request.TotalSeats < 1)
            throw new ApiException(ErrorCodes.InvalidInput, "Total seats must be at least 1.", 400);

        var result = await _svc.CreateClassAsync(name, request.TotalSeats.Value, ct);
        return Created($"/classes/{result.Id}", result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await _svc.GetClassesAsync(ct);
        return Ok(list);
    }

    [HttpGet("{classId}")]
    public async Task<IActionResult> Get(string classId, CancellationToken ct)
    {
        var result = await _svc.GetClassAsync(classId, ct);
        return Ok(result);
    }

    [HttpPost("{classId}/holds")]
    public async Task<IActionResult> CreateHold(string classId, CancellationToken ct)
    {
        var studentId = GetStudentId();
        var result = await _svc.CreateHoldAsync(classId, studentId, ct);
        return Created($"/holds/{result.HoldId}", result);
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