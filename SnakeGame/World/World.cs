using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnakeGame
{
    public class World
    {
        Dictionary<int, Snake> Player { get; set; }
        Dictionary<int, Wall> Walls { get; set; }
        Dictionary<int, Powerup> Powerups { get; set; }

        private int size { get; set; }

        public World(int _size)
        {

            Player = new Dictionary<int, Snake>();
            Walls = new Dictionary<int, Wall>();
            Powerups = new Dictionary<int, Powerup>();
            this.size = _size;
        }
    }
}
