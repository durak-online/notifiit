using System.Globalization;
 using Microsoft.EntityFrameworkCore;
 using NotiFIITBot.Consts;
 using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;
using NotiFIITBot.Domain;

namespace NotiFIITBot.Repo
{
    public class ScheduleRepository : IScheduleRepository
    {
        private readonly ScheduleDbContext _context;

        public ScheduleRepository(ScheduleDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<LessonModel>> UpsertLessonsAsync(IEnumerable<Lesson> lessons,
            CancellationToken ct = default)
        {
            var result = new List<LessonModel>();

            foreach (var lesson in lessons)
            {
                if (lesson.LessonId == null) continue;

                var lessonId = lesson.LessonId.Value;

                // 1. Ищем запись
                var existing = await _context.Lessons.FindAsync(new object[] { lessonId }, ct);

                if (existing == null)
                {
                    // 2. Создаем новую
                    var newModel = new LessonModel
                    {
                        LessonId = lessonId,
                        MenGroup = lesson.MenGroup,
                        SubGroup = lesson.SubGroup,
                        SubjectName = lesson.SubjectName?.Trim(),
                        TeacherName = lesson.TeacherName?.Trim(),
                        ClassroomNumber = lesson.ClassRoom,
                        AuditoryLocation = lesson.AuditoryLocation,

                        PairNumber = lesson.PairNumber ?? 0,
                        DayOfWeek = lesson.DayOfWeek ?? DayOfWeek.Monday,
                        Evenness = lesson.EvennessOfWeek,
                    };

                    await _context.Lessons.AddAsync(newModel, ct);
                    result.Add(newModel);
                }
                else
                {
                    // 3. Обновляем существующую
                    existing.MenGroup = lesson.MenGroup;
                    existing.SubGroup = lesson.SubGroup;
                    existing.SubjectName = lesson.SubjectName?.Trim();
                    existing.TeacherName = lesson.TeacherName?.Trim();
                    existing.ClassroomNumber = lesson.ClassRoom;
                    existing.AuditoryLocation = lesson.AuditoryLocation;

                    existing.PairNumber = lesson.PairNumber ?? 0;
                    existing.DayOfWeek = lesson.DayOfWeek ?? DayOfWeek.Monday;
                    existing.Evenness = lesson.EvennessOfWeek;

                    result.Add(existing);
                }
            }

            await _context.SaveChangesAsync(ct);
            return result;
        }

public async Task<List<LessonModel>> GetScheduleAsync(
    int groupNumber,
    int? subGroup,
    IScheduleRepository.SchedulePeriod period,
    DateTime? now = null,
    CancellationToken ct = default)
{
    now ??= DateTime.Now;

    var weekNumber = ISOWeek.GetWeekOfYear(now.Value);
    var currentEvenness = (weekNumber % 2 == 0) ? Evenness.Even : Evenness.Odd;

    IQueryable<LessonModel> q = _context.Lessons.AsNoTracking()
        .Where(l => l.MenGroup == groupNumber);

    if (subGroup.HasValue)
        q = q.Where(l => l.SubGroup == subGroup.Value || l.SubGroup == null);

    List<DayOfWeek> daysToLoad = period switch
    {
        IScheduleRepository.SchedulePeriod.Today => new() { now.Value.DayOfWeek },
        IScheduleRepository.SchedulePeriod.Tomorrow => new() { now.Value.AddDays(1).DayOfWeek },
        IScheduleRepository.SchedulePeriod.Week => Enum.GetValues<DayOfWeek>().ToList(),
        IScheduleRepository.SchedulePeriod.TwoWeeks => Enum.GetValues<DayOfWeek>().ToList(),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    // Фильтруем по дням недели
    q = q.Where(l => daysToLoad.Contains(l.DayOfWeek));

    var allLessons = await q.OrderBy(l => l.DayOfWeek)
                            .ThenBy(l => l.PairNumber)
                            .ToListAsync(ct);

    // Преобразуем в Lesson для работы с ChangeParity
    var lessonsForParity = allLessons.Select(l => new Lesson(
        pairNumber: l.PairNumber,
        subjectName: l.SubjectName?.Trim(),
        teacherName: l.TeacherName?.Trim(),
        classRoom: l.ClassroomNumber,
        begin: null,                 
        end: null,                    
        auditoryLocation: l.AuditoryLocation,
        subGroup: l.SubGroup ?? 0,    
        menGroup: l.MenGroup ?? 0,
        evennessOfWeek: l.Evenness,
        dayOfWeek: l.DayOfWeek
    ));

    // Объединяем пары с разной четностью
    var mergedLessons = ChangeParity(lessonsForParity);

    // Фильтруем по текущей неделе, но сохраняем Always
    var filteredLessons = mergedLessons.Where(l =>
        l.EvennessOfWeek == Evenness.Always || l.EvennessOfWeek == currentEvenness || l.EvennessOfWeek == null)
        .Select(l => new LessonModel
        {
            LessonId = l.LessonId,
            MenGroup = l.MenGroup,
            SubGroup = l.SubGroup,
            SubjectName = l.SubjectName,
            TeacherName = l.TeacherName,
            ClassroomNumber = l.ClassRoom,
            PairNumber = l.PairNumber ?? 0,
            DayOfWeek = l.DayOfWeek ?? DayOfWeek.Monday,
            Evenness = l.EvennessOfWeek
        })
        .OrderBy(l => l.DayOfWeek)
        .ThenBy(l => l.PairNumber)
        .ToList();

    return filteredLessons;
}

        private static IEnumerable<Lesson> ChangeParity(IEnumerable<Lesson> lessons)
        {
            var groups = lessons.GroupBy(l =>
                $"{l.SubjectName}-{l.TeacherName}-{l.ClassRoom}-{l.PairNumber}-{l.SubGroup}-{l.MenGroup}");
            var result = new List<Lesson>();

            foreach (var group in groups)
            {
                var list = group.ToList();
                if (list.Count == 1)
                {
                    result.Add(list[0]);
                    continue;
                }
                var hasOdd = list.Any(x => x.EvennessOfWeek == Evenness.Odd);
                var hasEven = list.Any(x => x.EvennessOfWeek == Evenness.Even);
                var merged = list[0];
                if (hasOdd && hasEven)
                    merged.EvennessOfWeek = Evenness.Always;
                result.Add(merged);
            }
            return result;
        }
    }

}
