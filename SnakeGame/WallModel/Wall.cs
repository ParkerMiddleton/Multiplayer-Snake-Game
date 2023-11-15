using System.Numerics;
using System.Text.Json.Serialization;

namespace SnakeGame
{
    public class Wall
    {
        [JsonInclude]
        private int wall;

        [JsonInclude]
        private Vector2D p1;

        [JsonInclude]
        private Vector2D p2;


    }
}