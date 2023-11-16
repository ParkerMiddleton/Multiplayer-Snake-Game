using System.Net;
using System.Text.Json.Serialization;

namespace SnakeGame
{
    public class Snake
    {
        [JsonInclude]
        public int snake { get; set; }

        [JsonInclude]
        public  string name { get; set; }

        [JsonInclude]
        public List<Vector2D> body { get; set; }

        [JsonInclude]
        public Vector2D dir { get; set; }

        [JsonInclude]
        public int score { get; set; }

        [JsonInclude]
        public bool died { get; set; }

        [JsonInclude]
        public bool alive { get; set; }

        [JsonInclude]
        public bool dc { get; set; }

        [JsonInclude]
        public bool join { get; set; }

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