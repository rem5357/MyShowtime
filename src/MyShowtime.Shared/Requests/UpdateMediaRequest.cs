using System.ComponentModel.DataAnnotations;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Shared.Requests;

public class UpdateMediaRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int Priority { get; set; }

    [Required]
    public ViewState WatchState { get; set; }

    public bool Hidden { get; set; }

    [MaxLength(64)]
    public string? Source { get; set; }

    [MaxLength(64)]
    public string? AvailableOn { get; set; }

    [MaxLength(4096)]
    public string? Notes { get; set; }
}
