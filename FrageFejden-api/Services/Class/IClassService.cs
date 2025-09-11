using FrageFejden.Entities;
using FrageFejden.Entities.Enums;

namespace FrageFejden_api.Services;

public sealed record CreateClassRequest(string Name, string? GradeLabel, string? Description = null, bool MakeMeTeacher = true);
public sealed record UpdateClassRequest(string Name, string? GradeLabel, string? Description);
public sealed record JoinRequest(string JoinCode);


public sealed record MyClassDto(
    Guid ClassId, Role RoleInClass, DateTime EnrolledAt,
    Guid Id, string Name, string? GradeLabel, string JoinCode, Guid? CreatedById, DateTime CreatedAt);

public sealed record MemberDto(Guid UserId, Role RoleInClass, DateTime EnrolledAt, Guid Id, string FullName, string Email, string? AvatarUrl);
public sealed record JoinedClassDto(Guid Id, string Name, string? GradeLabel);

public sealed class ScoreDto
{
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public double Score { get; set; }
}

public interface IClassService
{
    Task<(Guid Id, string Name)?> FindClassByJoinCodeAsync(string joinCode, CancellationToken ct);


    // Queries
    Task<(IReadOnlyList<Class> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<Class?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MyClassDto>> GetMyClassesAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<MemberDto>> GetMembersAsync(Guid classId, Guid requesterId, CancellationToken ct);

    // Commands
    Task<Class> CreateAsync(Guid ownerId, CreateClassRequest req, CancellationToken ct);
    Task<bool> UpdateAsync(Guid id, Guid requesterId, UpdateClassRequest req, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid requesterId, CancellationToken ct);

    Task<JoinedClassDto?> JoinByCodeAsync(Guid userId, string joinCode, CancellationToken ct);
    Task<bool> LeaveAsync(Guid userId, Guid classId, CancellationToken ct);
    Task<bool> AddMemberAsync(Guid classId, Guid requesterId, Guid userId, Role role, CancellationToken ct);
    Task<bool> RemoveMemberAsync(Guid classId, Guid requesterId, Guid userId, CancellationToken ct);
    Task<(Guid Id, string JoinCode)?> RegenerateJoinCodeAsync(Guid classId, Guid requesterId, CancellationToken ct);

    // Helper
    Task<bool> IsTeacherOrOwnerAsync(Guid classId, Guid userId, CancellationToken ct);


    Task<double?> GetPointsForUser(Guid userId, CancellationToken ct);
    Task<List<ScoreDto>> GetScoresForClassAsync(Guid userId, Guid classId, CancellationToken ct);
}
