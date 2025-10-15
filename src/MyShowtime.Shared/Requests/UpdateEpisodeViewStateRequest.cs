using System.ComponentModel.DataAnnotations;
using MyShowtime.Shared.Enums;

namespace MyShowtime.Shared.Requests;

public class UpdateEpisodeViewStateRequest
{
    [Required]
    public ViewState WatchState { get; set; }
}
