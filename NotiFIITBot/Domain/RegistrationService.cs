using Microsoft.Extensions.Caching.Memory;

namespace NotiFIITBot.Domain;

public class RegistrationService(IMemoryCache memoryCache)
{
    private readonly IMemoryCache cache = memoryCache;
    private readonly TimeSpan sessionLifetime = TimeSpan.FromMinutes(5);
    
    public void StartRegSession(long userId)
    {
        var session = new RegistrationSession { Step = RegistrationStep.SelectCourse };
        cache.Set(GetKey(userId), session, sessionLifetime);
    }

    public RegistrationSession? GetRegSession(long userId)
    {
        cache.TryGetValue(GetKey(userId), out RegistrationSession? session);
        return session;
    }

    public void UpdateRegSession(long userId, RegistrationSession session)
    {
        cache.Set(GetKey(userId), session, sessionLifetime);
    }
    
    public void RemoveUser(long userId) => cache.Remove(GetKey(userId));
    
    public bool IsUserRegistrating(long userId) => cache.TryGetValue(GetKey(userId), out _);
    
    private static string GetKey(long userId) => $"reg_{userId}";
}

public enum RegistrationStep
{
    SelectCourse,
    SelectGroup,
    SelectSubgroup
}

public class RegistrationSession
{
    public RegistrationStep Step { get; set; } = RegistrationStep.SelectCourse;
    public int SelectedCourse { get; set; }
    public int SelectedGroup { get; set; }
}

