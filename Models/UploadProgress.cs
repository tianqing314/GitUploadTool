namespace GitUploadTool.Models;

public enum UploadStep
{
    Init,
    Add,
    Commit,
    Push,
    Complete
}

public enum StepStatus
{
    Pending,
    Running,
    Success,
    Failed
}

public class UploadProgress
{
    public UploadStep Step { get; set; }
    public StepStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
