using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ranger.Services.Projects.Data;

namespace Ranger.Services.Projects.Controllers
{
    [ApiController]
    public class DatabaseUsernameController : ControllerBase
    {
        private readonly IProjectUniqueContraintRepository projectUniqueContraintRepository;
        private readonly ILogger<ProjectController> logger;

        public DatabaseUsernameController(IProjectUniqueContraintRepository projectUniqueContraintRepository, ILogger<ProjectController> logger)
        {
            this.projectUniqueContraintRepository = projectUniqueContraintRepository;
            this.logger = logger;
        }

        [HttpGet("project/{apikey}/databaseusername")]
        public async Task<IActionResult> GetDbUsernameByApiKey([FromRoute] string apiKey)
        {
            Guid parsedApiKey;
            if (!Guid.TryParse(apiKey, out parsedApiKey))
            {
                return BadRequest("Unable to parse the Api Key");
            }
            var dbUsername = await projectUniqueContraintRepository.GetDatabaseUsernameByApiKeyAsync(parsedApiKey);
            if (String.IsNullOrWhiteSpace(dbUsername))
            {
                return NotFound();
            }
            return Ok(new { DatabaseUsername = dbUsername });
        }
    }
}