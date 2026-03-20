using Microsoft.AspNetCore.Mvc;
using DialysisServer.Data;

namespace DialysisServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SensorController : ControllerBase
{
    private readonly AppDbContext _context;

    public SensorController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public IActionResult AddData()
    {
        return Ok("bruh");
    }
}