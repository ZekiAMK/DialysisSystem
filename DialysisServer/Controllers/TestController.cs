using DialysisServer.Data;
using DialysisServer.Models;
using Microsoft.AspNetCore.Mvc;
using Shared.Library;

namespace DialysisServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly AppDbContext _context;

    public TestController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Route("status")]
    public IActionResult Get()
    {
        return Ok("Dialysis system backend is running");
    }
    
    [HttpGet]
    [Route("default")]
    public IActionResult AddDefaultData()
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

    [HttpPost]
    [Route("add")]
    public IActionResult AddData([FromBody] SensorDataDTO data)
    {
        var fordata = new SensorData
        {
            Cadence = data.Cadence,
            Speed = data.Speed,
            Timestamp = DateTime.Now
        };

        _context.SensorData.Add(fordata);
        _context.SaveChanges();

        return Ok("Saved");
    }

    [HttpGet]
    [Route("all")]
    public IActionResult GetAllData()
    {
        var result = _context.SensorData.ToList();
        return Ok(result);
    }
}