using System.Numerics;

namespace Fc25Draft.Web.Data.Entities
{
    public class Position
    {
        public short PositionId { get; set; }
        public string Name { get; set; } = null!;
        public ICollection<Player> Players { get; set; } = new List<Player>();
    }
}
