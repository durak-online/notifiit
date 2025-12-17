using Microsoft.Extensions.Caching.Memory;

namespace NotiFIITBot.Domain;

public class RegistrationService(IMemoryCache memoryCache)
{
    private readonly IMemoryCache cache = memoryCache;
    private readonly TimeSpan sessionLifetime = TimeSpan.FromMinutes(5);

    public void AddUser(long userId) => cache.Set(GetKey(userId), true, sessionLifetime);
    
    public void RemoveUser(long userId) => cache.Remove(GetKey(userId));
    
    public bool ContainsUser(long userId) => cache.TryGetValue(GetKey(userId), out _);
    
    private static string GetKey(long userId) => $"{userId}";
}
