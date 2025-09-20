namespace PhainonDistributionCenter.Abstractions.Entities;

public interface IObjectWithTime
{
    /// <summary>
    /// 对象创建时间
    /// </summary>
    public DateTimeOffset CreatedTime { get; }
    
    /// <summary>
    /// 对象上次修改时间
    /// </summary>
    public DateTimeOffset UpdatedTime { get; }
}