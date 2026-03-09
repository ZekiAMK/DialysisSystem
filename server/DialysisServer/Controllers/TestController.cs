using Microsoft.AspNetCore.Mvc;

namespace DialysisServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Dialysis system backend is running");
    }
}