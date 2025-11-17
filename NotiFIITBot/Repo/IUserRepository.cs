using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NotiFIITBot.Database.Data;
using NotiFIITBot.Database.Models;

namespace NotiFIITBot.Database.Repositories
{
    // ===========================
    // ===== I N T E R F A C E ===
    // ===========================
    public interface IUserRepository
    {
        Task<List<User>> UpsertUsersAsync(IEnumerable<User> users, CancellationToken ct = default);
        Task<User> UpsertUserAsync(User user, CancellationToken ct = default);
        Task<List<User>> GetAllUsersAsync(CancellationToken ct = default);
        Task<bool> DeleteUserAsync(long telegramId, CancellationToken ct = default);
    }

    // ===========================
    // ===== I M P L E M E N T ===
    // ===========================
    public class UserRepository : IUserRepository
    {
        private readonly ScheduleDbContext _context;

        public UserRepository(ScheduleDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<User>> UpsertUsersAsync(IEnumerable<User> users, CancellationToken ct = default)
        {
            if (users == null) throw new ArgumentNullException(nameof(users));

            var result = new List<User>();

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                foreach (var user in users)
                {
                    var saved = await UpsertUserAsync(user, ct);
                    result.Add(saved);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return result;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<User> UpsertUserAsync(User user, CancellationToken ct = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var existing = await _context.Users
                .FirstOrDefaultAsync(u => u.TelegramId == user.TelegramId, ct);

            if (existing != null)
            {
                existing.GroupNumber = user.GroupNumber ?? existing.GroupNumber;
                existing.SubGroupNumber = user.SubGroupNumber ?? existing.SubGroupNumber;
                existing.NotificationsEnabled = user.NotificationsEnabled;
                existing.GlobalNotificationMinutes = user.GlobalNotificationMinutes;
                return existing;
            }

            await _context.Users.AddAsync(user, ct);
            return user;
        }

        public Task<List<User>> GetAllUsersAsync(CancellationToken ct = default)
        {
            return _context.Users.AsNoTracking().ToListAsync(ct);
        }

        public async Task<bool> DeleteUserAsync(long telegramId, CancellationToken ct = default)
        {
            var existing = await _context.Users.FirstOrDefaultAsync(u => u.TelegramId == telegramId, ct);
            if (existing == null) return false;

            _context.Users.Remove(existing);
            await _context.SaveChangesAsync(ct);
            return true;
        }
    }
}
