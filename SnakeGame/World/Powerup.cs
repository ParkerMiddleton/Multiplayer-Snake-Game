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

        [JsonConstructor]
        public Powerup(int _power, Vector2D _loc, bool _died)
        {
            this.power = _power;
            this.loc = _loc;
            this.died = _died;

        }



    }
}