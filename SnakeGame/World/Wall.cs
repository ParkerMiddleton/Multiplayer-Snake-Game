using System.Numerics;
using System.Text.Json.Serialization;

namespace SnakeGame
{
    public class Wall
    {
        [JsonInclude]
        public int wall { get; set; }

        [JsonInclude]
        public Vector2D p1 { get; set; }

        [JsonInclude]
        public Vector2D p2 { get; set; }

        [JsonConstructor]
        public Wall(int _wall, Vector2D _p1, Vector2D _p2)
        {
            this.wall = _wall;
            this.p1 = _p1;
            this.p2 = _p2;

        }

        


    }
}