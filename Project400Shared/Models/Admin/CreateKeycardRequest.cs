namespace Project400.Shared.Models.Admin;

public class CreateKeycardRequest
{
    public string CardUid { get; set; } = "";
    public Guid UserId { get; set; }
}
