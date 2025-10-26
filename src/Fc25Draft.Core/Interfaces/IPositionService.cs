using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface IPositionService
{
    Task<IReadOnlyList<Position>> GetAllAsync();
}
