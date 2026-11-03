using System.ComponentModel.DataAnnotations;

namespace Techinal_Exam.Models
{
    public class Documents
    {
        [Key]
        public Guid Id { get; set; }

        [Required, MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime DateandTime { get; set; }
    }
}
