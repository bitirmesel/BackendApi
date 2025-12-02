using System.ComponentModel.DataAnnotations;

namespace DktApi.Dtos.Tasks
{
    public class AssignTaskRequest
    {
        [Required]
        public long TherapistId { get; set; }

        [Required]
        public long PlayerId { get; set; }

        [Required]
        public long GameId { get; set; }

        [Required]
        public long LetterId { get; set; }

        public long? AssetSetId { get; set; }

        public DateTime? DueAt { get; set; }

        public string? Note { get; set; }
    }
}
