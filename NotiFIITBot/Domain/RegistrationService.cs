namespace NotiFIITBot.Domain;

public class RegistrationService
{
    private readonly HashSet<long> registeringUserIds = new();

    public void AddUser(long userId) => registeringUserIds.Add(userId);
    public void RemoveUser(long userId) => registeringUserIds.Remove(userId);
    public bool ContainsUser(long userId) => registeringUserIds.Contains(userId);
}
