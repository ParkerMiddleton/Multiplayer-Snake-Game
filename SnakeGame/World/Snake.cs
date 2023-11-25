using System.Net;
using System.Text;
using System.Text.Json.Serialization;

namespace SnakeGame;

/// <summary>
/// <Author>Abbey Lasater</Author>
/// <Author>Parker Middleton</Author>
/// <Date>November 24th, 2023</Date>
/// The Snake class represents the Snakes in the world.
/// </summary>
public class Snake
{
    /// <summary>
    /// Getter/Setter method for snake ID.
    /// </summary>
    public int snake { get; set; }
    /// <summary>
    /// Getter/Setter method for snake name.
    /// </summary>
    public string name { get; set; }
    /// <summary>
    /// Getter/Setter method for location body segments location.
    /// </summary>
    public List<Vector2D> body { get; set; }
    /// <summary>
    /// Getter/Setter method for direction body segments location.
    /// </summary>
    public Vector2D dir { get; set; }
    /// <summary>
    /// Getter/Setter method for snakes score.
    /// </summary>
    public int score { get; set; }
    /// <summary>
    /// Getter/Setter method for whether snake is alive or dead.
    /// </summary>
    public bool died { get; set; }
    /// <summary>
    /// Getter/Setter method for whether snake is alive or dead.
    /// </summary>
    public bool alive { get; set; }
    /// <summary>
    /// Getter/Setter method for whether snake has disconnected
    /// </summary>
    public bool dc { get; set; }
    /// <summary>
    /// Getter/Setter method for whether snake has joined.
    /// </summary>
    public bool join { get; set; }

    /// <summary>
    /// Parameterized Json constructor for Snake Class -- used for Json deserialization.
    /// </summary>
    /// <param name="snake">unique id of snake</param>
    /// <param name="name">name of snake</param>
    /// <param name="body">location of snake (x and y coord), and each segment of snake</param>
    /// <param name="dir"> direction snake is facing; up, down, left, right</param>
    /// <param name="score">score of snake -- amt of powerups eaten.</param>
    /// <param name="died"> bool true or false, false if snake is alive, true if snake died</param>
    /// <param name="alive">bool true or false, false if snake is dead, true if snake alive</param>
    /// <param name="dc">bool true or false, false if snake is still on, true if snake has disconnected</param>
    /// <param name="join">bool true or false, false if snake did not just join on frame, true if snake joined on frame</param>
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
