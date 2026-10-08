using BehavioralAnticheatEngine.Application.Common.Interfaces;
using BehavioralAnticheatEngine.Application.Common.Mediation;
using BehavioralAnticheatEngine.Application.Users.Contracts;
using BehavioralAnticheatEngine.Domain.Identity;

namespace BehavioralAnticheatEngine.Application.Users;

public sealed record ListStudentsQuery : IAppRequest<IReadOnlyCollection<UserDto>>;

internal sealed class ListStudentsHandler : IAppRequestHandler<ListStudentsQuery, IReadOnlyCollection<UserDto>>
{
    private readonly IUserRepository _users;

    public ListStudentsHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<IReadOnlyCollection<UserDto>> HandleAsync(ListStudentsQuery request, CancellationToken cancellationToken)
    {
        var users = await _users.ListByRoleAsync(UserRoles.Student, cancellationToken);

        return users.Select(user => new UserDto(user.Id, user.Email, user.Role)).ToArray();
    }
}
