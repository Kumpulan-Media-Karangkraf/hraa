
using System.ComponentModel.DataAnnotations.Schema;

namespace HRAnalysis.Models
    {
    public class v_HRA_AttKesalahan
    {
        public string? idstaff { get; set; }
        public string? Nama { get; set; }
        public DateTime Tarikh { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }
        public DateTime? Trk_Masuk { get; set; }
        public string? StatusIn { get; set; }
        public string? StatusOut { get; set; }
        public string? Syrt { get; set; }
        public string? Jab { get; set; }
        public string? Bhgn { get; set; }
        public string? Jaw { get; set; }
        public DateTime? Roster_TImeIn { get; set; }
        public DateTime? Roster_Timeout { get; set; }

        public int? valIn { get; set; }
        public int? valOut { get; set; }

        public int? ACTValIn { get; set; }
        public int? ACTValOut { get; set; }

    }
}
