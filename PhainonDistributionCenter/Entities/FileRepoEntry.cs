using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

public class FileRepoEntry : IObjectWithTime
{
    [MaxLength(64)]
    [Column(TypeName = "binary(64)")]
    [Key]
    public byte[] FileSha512 { get; set; } = [];
    
    [MaxLength(64)]
    public byte[] ArchiveSha512 { get; set; } = [];

    public string FileName { get; set; } = "";

    public string ArchiveDownloadUrl { get; set; } = "";
    
    public DateTime CreatedTime { get; } = DateTime.Now;
    public DateTime UpdatedTime { get; } = DateTime.Now;
}