// DTOs/ScheduleQueryDto.cs
using Microsoft.AspNetCore.Mvc;

namespace NotiFIITBot.Api.DTOs;

public class ScheduleQueryDto
{
    [FromQuery(Name = "groupId")]
    public int MenGroupId { get; set; }

    [FromQuery(Name = "subgroup")]
    public int? Subgroup { get; set; } // (0 - общая, 1 - первая, 2 - вторая)

    [FromQuery(Name = "specificDate")]
    public DateTime? SpecificDate { get; set; }

    [FromQuery(Name = "startDate")]
    public DateTime? StartDate { get; set; }
    [FromQuery(Name = "endDate")]
    public DateTime? EndDate { get; set; }
}