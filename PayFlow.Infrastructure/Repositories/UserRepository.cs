using Microsoft.EntityFrameworkCore;
using PayFlow.Application.Interfaces;
using PayFlow.Domain.Entities;
using PayFlow.Infrastructure.Data;

namespace PayFlow.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PayFlowDbContext _context;

    public UserRepository(PayFlowDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        => await _context.Users.AddAsync(user, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
