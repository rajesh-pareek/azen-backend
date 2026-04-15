using Microsoft.AspNetCore.Mvc;
namespace Azen.Api.Controllers;


[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _config;

    public HealthController(IConfiguration config)
    {
        _config = config;
    }
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(
            new
            {
                service = _config["App:Name"],
                version = _config["App:Version"],
                status = "running",
                timestamp = DateTime.UtcNow
            }
        );
    }
}
