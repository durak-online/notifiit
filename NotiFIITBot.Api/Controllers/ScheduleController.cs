using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Api.DTOs;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Domain;

namespace NotiFIITBot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController(ScheduleDbContext dbContext) : ControllerBase
{
    private readonly ScheduleDbContext dbContext = dbContext;

    /// <summary>
    /// Получить расписание по группе, подгруппе и дате/периоду.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSchedule([FromQuery] ScheduleQueryDto query)
    {
        // Валидация
        if (query.MenGroupId <= 0)
            return BadRequest($"GroupId ({query.MenGroupId}) must be positive");
        if (query.Subgroup.HasValue && (query.Subgroup < 0 || query.Subgroup > 2))
            return BadRequest("Subgroup must be 0, 1 or 2");

        DateTime[] targetDates;
        if (query.SpecificDate.HasValue)
        {
            targetDates = [query.SpecificDate.Value.Date];
        }
        else if (query.StartDate.HasValue && query.EndDate.HasValue)
        {
            var start = query.StartDate.Value.Date;
            var end = query.EndDate.Value.Date;
            targetDates = Enumerable.Range(0, (end - start).Days + 1)
                                    .Select(offset => start.AddDays(offset))
                                    .ToArray();
        }
        else
        {
            return BadRequest("Provide either SpecificDate or both StartDate and EndDate");
        }

        var lessonsQuery = dbContext.Lessons
            .Where(l => l.MenGroup == query.MenGroupId)
            .AsNoTracking();

        if (query.Subgroup.HasValue)
            lessonsQuery = lessonsQuery.Where(l => l.SubGroup == 0 || l.SubGroup == query.Subgroup);

        var allLessons = await lessonsQuery.ToListAsync();
        Console.WriteLine($"Count: {allLessons.Count}");

        var result = new List<LessonResponseDto>();
        foreach (var date in targetDates)
        {
            var dayOfWeek = date.DayOfWeek;
            var evenness = DateOnly.FromDateTime(date).GetEvenness();

            var lessonsForDate = allLessons.Where(l =>
                l.DayOfWeek == dayOfWeek &&
                (l.Evenness == evenness || l.Evenness == Consts.Evenness.Always)
            );

            foreach (var lesson in lessonsForDate)
            {
                result.Add(new LessonResponseDto
                {
                    Date = date,
                    DayOfWeek = dayOfWeek.ToString(),
                    PairNumber = lesson.PairNumber,
                    StartTime = lesson.StartTime.ToString(),
                    EndTime = lesson.EndTime.ToString(),
                    SubjectName = lesson.SubjectName,
                    TeacherName = lesson.TeacherName,
                    ClassroomNumber = lesson.ClassroomNumber,
                    AuditoryLocation = lesson.AuditoryLocation,
                    Evenness = lesson.Evenness.ToString()
                });
            }
        }

        result = result
            .OrderBy(r => r.Date)
            .ThenBy(r => r.PairNumber)
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Альтернативный метод: получить расписание на конкретную дату через параметр в URL.
    /// GET /api/schedule/2025-04-15?groupId=123&subgroup=1
    /// </summary>
    [HttpGet("{date}")]
    public async Task<IActionResult> GetScheduleByDate(
        string date,
        [FromQuery] int groupId,
        [FromQuery] int? subgroup)
    {
        if (!DateOnly.TryParse(date, out var dateOnly))
            return BadRequest("Invalid date format. Use yyyy-MM-dd");

        var query = new ScheduleQueryDto
        {
            MenGroupId = groupId,
            Subgroup = subgroup,
            SpecificDate = dateOnly.ToDateTime(TimeOnly.MinValue)
        };

        return await GetSchedule(query);
    }
}