using System.Numerics;
using System.Text.Json.Serialization;

namespace SnakeGame
{
    /// <summary>
    /// The Walls class represents the Walls in the world.
    /// Contains getters and setters for wall id, starting point of wall, ending point of wall and Json contructor.
    /// </summary>
    public class Wall
    {
        /// <summary>
        /// Getter/setter method for wall ID.
        /// </summary>
        public int wall { get; set; }
        /// <summary>
        /// Getter/setter method for starting point of wall
        /// </summary>
        public Vector2D p1 { get; set; }
        /// <summary>
        /// Getter/setter method for end point of wall
        /// </summary>
        public Vector2D p2 { get; set; }

        /// <summary>
        /// Parameterized Json constructor for Wall Class -- used for Json deserialization.
        /// </summary>
        /// <param name="wall" int representing the walls unique ID></param>
        /// <param name="p1" starting point of wall as a vector2D></param>
        /// <param name="p2" ending point of wall as a vector2D></param>
        [JsonConstructor]
        public Wall(int wall, Vector2D p1, Vector2D p2)
        {
            this.wall = wall;
            this.p1 = p1;
            this.p2 = p2;
        }
    }
}