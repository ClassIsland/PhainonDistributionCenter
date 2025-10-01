using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

public class AccessTokenInfo : IObjectWithTime
{
    [Key]
    public Guid Id { get; set; }


    [MaxLength(32)]
    public string Name { get; set; } = "";
    
    [MaxLength(32)]
    public byte[] TokenHash { get; set; } = [];

    public bool IsActive { get; set; } = false;

    public int CreatorUid { get; set; } = 0;

    public string CreatorName { get; set; } = "";
    
    public DateTimeOffset ExpireTime { get; set; } = DateTimeOffset.UtcNow;
    
    public DateTimeOffset CreatedTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedTime { get; set; } = DateTimeOffset.UtcNow;
}