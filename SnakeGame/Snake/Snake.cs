using System.Text.Json.Serialization;

namespace SnakeGame
{
    public class Snake
    {
        [JsonInclude]
        private int snake;

        [JsonInclude]
        private string name;

        [JsonInclude]
        private List<Vector2D> body;

        [JsonInclude]
        private Vector2D dir;

        [JsonInclude]
        private int score;

        [JsonInclude]
        private bool died;

        [JsonInclude]
        private bool alive;

        [JsonInclude]
        private bool dc;

        [JsonInclude]
        private bool join; 


    }
}