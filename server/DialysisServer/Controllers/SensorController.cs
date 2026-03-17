using Microsoft.AspNetCore.Mvc;
using DialysisServer.Data;
using DialysisServer.Models;

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
        var data = new SensorData
        {
            Cadence = 90,
            Speed = 30.2,
            Timestamp = DateTime.Now
        };

        _context.SensorData.Add(data);
        _context.SaveChanges();

        return Ok("Saved");
    }
}