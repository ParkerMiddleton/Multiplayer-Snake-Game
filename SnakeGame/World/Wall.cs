using System.Numerics;
using System.Text.Json.Serialization;

namespace SnakeGame
{
    public class Wall
    {
        public int wall { get; set; }
        public Vector2D p1 { get; set; }
        public Vector2D p2 { get; set; }

        [JsonConstructor]
        public Wall(int wall, Vector2D p1, Vector2D p2)
        {
            this.wall = wall;
            this.p1 = p1;
            this.p2 = p2;

        }

     

        


    }
}