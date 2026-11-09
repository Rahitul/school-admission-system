using Microsoft.AspNetCore.Mvc;
using SchoolAdmission.Dtos;
using SchoolAdmission.Services;

namespace SchoolAdmission.Controllers;

[ApiController]
[Route("students")]
public class StudentsController : ControllerBase
{
    private readonly AdmissionService _svc;

    public StudentsController(AdmissionService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await _svc.GetStudentsAsync(ct);
        return Ok(result);
    }
}