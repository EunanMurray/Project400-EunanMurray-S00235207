namespace Project400.Shared.Models.Device;

public class TailgateAnalysisResponse
{
    public bool IsTailgating { get; set; }
    public int PeopleDetected { get; set; }
    public double Confidence { get; set; }
    public string? AlertId { get; set; }
    public string? Error { get; set; }
}
