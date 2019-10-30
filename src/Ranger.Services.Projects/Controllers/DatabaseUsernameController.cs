using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Ranger.Common;
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

            if (apiKey.StartsWith("live.") || apiKey.StartsWith("test."))
            {
                var dbUsername = await projectUniqueContraintRepository.GetDatabaseUsernameByApiKeyAsync(apiKey);
                if (String.IsNullOrWhiteSpace(dbUsername))
                {
                    return NotFound();
                }
                return Ok(new { DatabaseUsername = dbUsername });
            }

            var apiErrorContent = new ApiErrorContent();
            apiErrorContent.Errors.Add("The API key does not have a valid prefix.");
            return BadRequest(apiErrorContent);
        }
    }
}