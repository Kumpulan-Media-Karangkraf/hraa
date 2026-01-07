namespace HRAnalysis.Models.ViewModel
{
    public class SemakanViewModel
    {
        public int IdSemakan { get; set; }
        public string IdStaff { get; set; }
        public string Name { get; set; }
        public DateTime Trk { get; set; }
        public string AplikasiName { get; set; }
        public int? IdAplikasi { get; set; }
        public string Catatan { get; set; }
        public DateTime? TarikhMula { get; set; }
        public DateTime? TarikhTamat { get; set; }
        public float? BilHari { get; set; }
    }
}
