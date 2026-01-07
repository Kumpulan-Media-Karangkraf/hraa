using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRAnalysis.Models
{
    public class tbl_ATTSemakanV1
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdSemakan { get; set; }

        [Required]
        public string IdStaff { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public DateTime Trk { get; set; } // Tarikh Semakan (e.g. tarikh kesalahan or semakan)

        public string? AplikasiName { get; set; } // "Borang A", "Cuti Umum", etc.

        public int? IdAplikasi { get; set; } // External ref to BorangA/C/other app

        public string? Catatan { get; set; } // Free notes

        public DateTime? TarikhMula { get; set; } // For Tugasan or long coverage
        public DateTime? TarikhTamat { get; set; }

        public double? BilHari { get; set; } // Total days covered (Tamat - Mula + 1)

        public int? valInNEW { get; set; }       // Optional: new value from semakan
        public int? valOutNEW { get; set; }

        public int? ACTValInNEW { get; set; }
        public int? ACTValOutNEW { get; set; }

        public string? UpdatedBy { get; set; }
        public int? IdProfile { get; set; }
    }
}
