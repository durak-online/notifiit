using System.Security.Cryptography;
using System.Text;
using NotiFIITBot.Consts;

namespace NotiFIITBot.Domain;

public static class LessonProcessor
{
    /// <summary>
    /// Шаг 1. Если у подгруппы 1 и 2 пара полностью совпадает (включая время и четность),
    /// то это пара для всей группы (SubGroup = 0).
    /// </summary>
    public static IEnumerable<Lesson> NormalizeSubgroups(IEnumerable<Lesson> lessons)
    {
        var groups = lessons.GroupBy(l => new
        {
            l.DayOfWeek,
            l.PairNumber,
            l.MenGroup,
            l.SubjectName,
            l.TeacherName,
            l.ClassRoom,
            l.EvennessOfWeek, 
            l.AuditoryLocation
        });

        foreach (var group in groups)
        {
            var list = group.ToList();

            var hasSub1 = list.Any(x => x.SubGroup == 1);
            var hasSub2 = list.Any(x => x.SubGroup == 2);

            // Если пара есть у ОБЕИХ подгрупп -> схлопываем в 0
            if (hasSub1 && hasSub2)
            {
                var commonLesson = list.First();
                commonLesson.SubGroup = 0; 
                yield return commonLesson;
            }
            else
            {
                foreach (var item in list)
                    yield return item;
            }
        }
    }

    /// <summary>
    /// Шаг 2. Если пара (уже с subGroup 0, 1 или 2) проходит и по четным, и по нечетным -> Evenness = Always.
    /// </summary>
    public static IEnumerable<Lesson> MergeByParity(IEnumerable<Lesson> lessons)
    {
        var groups = lessons.GroupBy(l =>
            $"{l.DayOfWeek}-{l.SubjectName}-{l.TeacherName}-{l.ClassRoom}-{l.PairNumber}-{l.SubGroup}-{l.MenGroup}");

        foreach (var group in groups)
        {
            var list = group.ToList();
            if (list.Count == 1)
            {
                yield return list[0];
                continue;
            }

            var hasOdd = list.Any(x => x.EvennessOfWeek == Evenness.Odd);
            var hasEven = list.Any(x => x.EvennessOfWeek == Evenness.Even);
            
            var merged = list[0];
            
            if (hasOdd && hasEven)
                merged.EvennessOfWeek = Evenness.Always;
            
            yield return merged;
        }
    }

    /// <summary>
    /// Генерация стабильного ID.
    /// </summary>
    public static void AssignStableIds(IEnumerable<Lesson> lessons)
    {
        using var sha256 = SHA256.Create();

        foreach (var l in lessons)
        {
            if (l == null) 
                continue;

            var gNum = l.MenGroup ?? 0;
            var sg = l.SubGroup ?? 0;
            var parityInt = (int)l.EvennessOfWeek;
            var day = (int)(l.DayOfWeek ?? DayOfWeek.Monday);
            var pair = l.PairNumber ?? 0;
            var room = l.ClassRoom ?? "0";

            var uniqueString = $"{gNum}_{sg}_{parityInt}_{day}_{pair}_{room}_{l.SubjectName}";
            
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(uniqueString));
            l.LessonId = new Guid(bytes.Take(16).ToArray());
        }
    }
}