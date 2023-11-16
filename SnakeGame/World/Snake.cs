using System.Net;
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

        [JsonConstructor]
        public Snake(int _snake, string _name, List<Vector2D> _body, Vector2D _dir, int _score, bool _died, bool _alive, bool _dc, bool _join)
        {
            this.snake = _snake;
            this.name = _name;
            this.body = _body; 
            this.dir = _dir;
            this.score = _score;
            this.died = _died;
            this.alive = _alive;
            this.dc = _dc;
            this.join = _join;
        }

    }
}