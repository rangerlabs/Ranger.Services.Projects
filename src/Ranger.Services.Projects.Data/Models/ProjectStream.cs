using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public class ProjectStream : RowLevelSecurityDbSet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public Guid StreamId { get; set; }
        [Required]
        [StringLength(28)]
        public string Domain { get; set; }
        [Required]
        public Guid ProjectId { get; set; }
        [Required]
        public Guid ApiKey { get; set; }
        [Required]
        public int Version { get; set; }
        [Required]
        public string ProjectData { get; set; }
        [Required]
        public string Event { get; set; }
        [Required]
        public DateTime InsertedAt { get; set; }
        [Required]
        public string InsertedBy { get; set; }
    }
}