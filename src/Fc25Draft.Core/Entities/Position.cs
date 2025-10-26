using System.Numerics;

namespace Fc25Draft.Core.Entities
{
    public class Position
    {
        public short PositionId { get; set; }
        public string Name { get; set; } = null!;
        public ICollection<Player> Players { get; set; } = new List<Player>();
    }
}
