using System.ComponentModel.DataAnnotations;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Shared.Requests;

public class ImportMediaRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int TmdbId { get; set; }

    [Required]
    public MediaType MediaType { get; set; }

    public int? Priority { get; set; }
}
