using System.Net;
using System.Text.Json.Serialization;

namespace SnakeGame
{
    public class Snake
    {
        public int snake { get; set; }
        public  string name { get; set; }
        public List<Vector2D> body { get; set; }
        public Vector2D dir { get; set; }
        public int score { get; set; }
        public bool died { get; set; }
        public bool alive { get; set; }
        public bool dc { get; set; }
        public bool join { get; set; }

        public Snake()
        {

        }

        [JsonConstructor]
        public Snake(int snake, string name, List<Vector2D> body, Vector2D dir, int score, bool died, bool alive, bool dc, bool join)
        {
            this.snake = snake;
            this.name = name;
            this.body = body; 
            this.dir = dir;
            this.score = score;
            this.died = died;
            this.alive = alive;
            this.dc = dc;
            this.join = join;
        }

    }
}