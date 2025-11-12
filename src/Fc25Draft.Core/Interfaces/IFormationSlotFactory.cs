using System.Collections.Generic;

namespace Fc25Draft.Core.Interfaces;

public readonly record struct FormationSlotTemplate(byte Role, int Order, int PrimaryPositionId);

public interface IFormationSlotFactory
{
    IReadOnlyList<FormationSlotTemplate> Build(string formationCode);
    bool Supports(string formationCode);
    IReadOnlyList<string> GetSupportedFormations();
}
