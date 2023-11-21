using System.Text.Json.Serialization;

namespace SnakeGame

{
    public class Powerup
    {
        public int power { get; set; }
        public Vector2D loc { get; set; }
        public bool died { get; set; }

        public Powerup()
        {

        }

        [JsonConstructor]
        public Powerup(int power, Vector2D loc, bool died)
        {
            this.power = power;
            this.loc = loc;
            this.died = died;

        }



    }
}