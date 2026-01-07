using System.ComponentModel.DataAnnotations;

namespace HRAnalysis.Models
{
    public class v_HRA_ATTSemakan_BorangTugasan
    {
        [Key]
        public int IDTugasan { get; set; }

        // Make this nullable to match actual database structure
        public string? NoPekerja { get; set; }
        public DateTime TarikhMula { get; set; }
        public DateTime TarikhTamat { get; set; }
        public string? Catatan { get; set; } // Also make this nullable for safety
    }
}