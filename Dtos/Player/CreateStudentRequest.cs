using System.ComponentModel.DataAnnotations;

namespace DktApi.Dtos.Player
{
    // POST /api/students için DTO
    public class CreateStudentRequest
    {
        // Öğrenciyi kimin eklediği – therapist_clients için zorunlu
        [Required]
        public long TherapistId { get; set; }

        // Öğrenci adı zorunlu
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // Opsiyonel alanlar
        public string? Nickname { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        // Eğer şifreli giriş yoksa boş gelebilir
        public string? Password { get; set; }

        // Öğrencinin doğum tarihi – opsiyonel
        public DateTime? BirthDate { get; set; }

        public string? Gender { get; set; }

        public string? Diagnosis { get; set; }

        public string? ParentName { get; set; }
        public string? ParentPhone { get; set; }

        public string? City { get; set; }
        public string? SchoolName { get; set; }

        // Öğrenci hakkında açıklama
        public string? Abouts { get; set; }
    }
}
