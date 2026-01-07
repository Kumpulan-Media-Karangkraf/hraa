namespace HRAnalysis.Models.ViewModel
{
    public class ProcessedRecordViewModel
    {
        public int IdSemakan { get; set; }
        public string IdStaff { get; set; }
        public string Name { get; set; }
        public DateTime Trk { get; set; }
        public DateTime TarikhMula { get; set; }
        public DateTime TarikhTamat { get; set; }
        public float BilHari { get; set; }
        public string Catatan { get; set; }
    }
}
