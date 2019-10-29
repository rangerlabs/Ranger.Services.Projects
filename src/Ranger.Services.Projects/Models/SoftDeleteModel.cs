using System.ComponentModel.DataAnnotations;

namespace Ranger.Services.Projects
{
    public class SoftDeleteModel
    {
        [Required]
        public string UserEmail { get; set; }
    }
}