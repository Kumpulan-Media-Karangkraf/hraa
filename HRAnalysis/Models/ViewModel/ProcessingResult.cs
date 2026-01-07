namespace HRAnalysis.Models.ViewModel
{
    public class ProcessingResult
    {
        public int ProcessedCount { get; set; } = 0;
        public int CreatedCount { get; set; } = 0;
        public List<ProcessedRecordViewModel> ProcessedRecords { get; set; } = new List<ProcessedRecordViewModel>();
    }
}
