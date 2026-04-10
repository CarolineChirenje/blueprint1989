namespace Batanai.Api.Models;

public class CycleMemberAgreement
{
    public int      Id             { get; set; }
    public int      ExpenseCycleId { get; set; }
    public int      UserId         { get; set; }
    public DateTime AgreedAt       { get; set; } = DateTime.UtcNow;
}
