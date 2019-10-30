using System.ComponentModel.DataAnnotations;

namespace Ranger.Services.Projects
{
    public class ApiKeyResetModel
    {
        [Required]
        public int Version { get; set; }
        [Required]
        public string UserEmail { get; set; }
    }
}