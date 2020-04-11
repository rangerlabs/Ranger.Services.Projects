using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ranger.Services.Projects
{
    public class ProjectUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid ProjectId { get; set; }

        [Required]
        public string TenantId { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public DateTime InsertedAt { get; set; }

        [Required]
        public string InsertedBy { get; set; }
    }
}