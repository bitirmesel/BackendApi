using System.Linq.Expressions;
using DktApi.Models.Db;

namespace DktApi.Dtos.Tasks
{
    /// <summary>
    /// Oyuncu görev listesi yanıtı: GET /api/players/{id}/tasks ve /tasks/active.
    /// Düz alanlar eski istemciler için korunur; iç içe <see cref="Game"/> ve <see cref="Therapist"/>
    /// Fonem'in görev → oyun yönlendirmesi (oyun tipi, zorluk) ve terapist bilgisi içindir.
    /// Alan eklemek serbest, var olanı kaldırmak/yeniden adlandırmak istemcileri kırar.
    /// </summary>
    public class PlayerTaskDto
    {
        public long TaskId { get; set; }
        public long GameId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public long LetterId { get; set; }
        public string LetterCode { get; set; } = string.Empty;
        public string LetterDisplayName { get; set; } = string.Empty;
        public long? AssetSetId { get; set; }
        public string? Note { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? AssignedAt { get; set; }
        public DateTime? DueAt { get; set; }

        public long TherapistId { get; set; }
        public string TherapistName { get; set; } = string.Empty;

        /// <summary>SYLLABLE | WORD | SENTENCE (game_types.code)</summary>
        public string GameTypeCode { get; set; } = string.Empty;

        /// <summary>1-3 (difficulty_levels.level)</summary>
        public int DifficultyLevel { get; set; }

        public PlayerTaskGameDto Game { get; set; } = new();
        public PlayerTaskTherapistDto Therapist { get; set; } = new();

        /// <summary>EF Core'un SQL'e çevirdiği projeksiyon (Include gerekmez).</summary>
        public static readonly Expression<Func<TaskItem, PlayerTaskDto>> Projection = t => new PlayerTaskDto
        {
            TaskId = t.Id,
            GameId = t.GameId,
            GameName = t.Game.Name,
            LetterId = t.LetterId,
            LetterCode = t.Letter.Code,
            LetterDisplayName = t.Letter.DisplayName,
            AssetSetId = t.AssetSetId,
            Note = t.Note,
            Status = t.Status,
            AssignedAt = t.AssignedAt,
            DueAt = t.DueAt,
            TherapistId = t.TherapistId,
            TherapistName = t.Therapist.Name,
            GameTypeCode = t.Game.GameType.Code,
            DifficultyLevel = t.Game.DifficultyLevel.Level,
            Game = new PlayerTaskGameDto
            {
                Id = t.GameId,
                Name = t.Game.Name,
                GameTypeId = t.Game.GameTypeId,
                DifficultyLevelId = t.Game.DifficultyLevelId,
                GameType = new PlayerTaskGameTypeDto
                {
                    Id = t.Game.GameType.Id,
                    Code = t.Game.GameType.Code,
                    Name = t.Game.GameType.Name
                },
                DifficultyLevel = new PlayerTaskDifficultyDto
                {
                    Id = t.Game.DifficultyLevel.Id,
                    Level = t.Game.DifficultyLevel.Level,
                    Name = t.Game.DifficultyLevel.Name
                }
            },
            Therapist = new PlayerTaskTherapistDto
            {
                Id = t.TherapistId,
                Name = t.Therapist.Name,
                ClinicName = t.Therapist.ClinicName
            }
        };
    }

    public class PlayerTaskGameDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long GameTypeId { get; set; }
        public long DifficultyLevelId { get; set; }
        public PlayerTaskGameTypeDto GameType { get; set; } = new();
        public PlayerTaskDifficultyDto DifficultyLevel { get; set; } = new();
    }

    public class PlayerTaskGameTypeDto
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class PlayerTaskDifficultyDto
    {
        public long Id { get; set; }
        public int Level { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Çocuğa gösterilecek kadar terapist bilgisi (e-posta/telefon yok).</summary>
    public class PlayerTaskTherapistDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ClinicName { get; set; }
    }
}
