using System.Text.Json.Serialization;

namespace SnakeGame

{
    public class Powerup
    {
        [JsonInclude]
        private int power;

        [JsonInclude]
        private Vector2D loc;

        [JsonInclude]
        private bool died; 



    }
}