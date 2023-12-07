using System.ComponentModel;
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

    //An invisible barrier surrounding the snakes head, if anything comes in contact with this barrier
    // then a collision will be triggered. 
    // 10 bc snakes are drawn at a width of 10 
    private readonly int collisonRadius = 5;

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
    /// Direction of the head of the snake.
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

    public int RespawnRate { get; set; }

    public int GrowthRate { get; set; }


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


    /// <summary>
    /// Used for initial snake construction upon connection to the server. 
    /// </summary>
    /// <param name="snake"></param>
    /// <param name="name"></param>
    public Snake(int snake, string name, int RespawnRateTimer, int growthRate)
    {
        this.snake = snake;
        this.name = name;
        body = new List<Vector2D>()
        {
            new Vector2D(0,1),
            new Vector2D(0,121)
        };
        dir = new Vector2D(0, 1);
        score = 0;
        died = false;
        alive = true;
        dc = false;
        join = true;  
        RespawnRate = RespawnRateTimer;
        GrowthRate = growthRate;
    }

    /// <summary>
    /// Getter for Collison Radius
    /// </summary>
    /// <returns></returns>
    public int GetCollisonRadius()
    {
        return collisonRadius;
    }


    /// <summary>
    /// Compares one snake to another based on their heads.
    /// It is not possible for two snake can have the same head coordinates 
    /// </summary>
    /// <param name="obj">A Snake object</param>
    /// <returns>boolean</returns>
    public override bool Equals(object? obj)
    {
        if(obj is not Snake)
        {
            return false;
        }
        Snake? snake = obj as Snake;
        return snake!.body.Last().Equals(this.body.Last());

    }


    /// <summary>
    /// Gives the hash code of the head of the snake, which should always never be the same. 
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return body.Last().GetHashCode();
    }
}
