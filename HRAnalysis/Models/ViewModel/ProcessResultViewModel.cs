namespace HRAnalysis.Models.ViewModel
{
    public class ProcessResultViewModel
    {
        public bool Success { get; set; }
        public int ProcessedCount { get; set; }
        public int CreatedCount { get; set; }
        public string Message { get; set; }
        public List<ProcessedRecordViewModel> ProcessedRecords { get; set; } = new List<ProcessedRecordViewModel>();
    }
}
