using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnakeGame
{
    public class World
    {
        public Dictionary<int, Snake> Players { get; set; }
        public Dictionary<int, Wall> Walls { get; set; }
        public Dictionary<int, Powerup> Powerups { get; set; }

        private int size { get; set; }

        public World(int _size)
        {
            Players = new Dictionary<int, Snake>();
            Walls = new Dictionary<int, Wall>();
            Powerups = new Dictionary<int, Powerup>();
            this.size = _size;
        }
    }
}
