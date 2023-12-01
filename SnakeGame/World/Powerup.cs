using System.Text.Json.Serialization;

namespace SnakeGame;

/// <summary>
/// <Author>Abbey Lasater</Author>
/// <Author>Parker Middleton</Author>
/// <Date>November 24th, 2023</Date>
/// The Powerup class represents the powerups in the world.
/// Contains getters and setters for power, loc, and died. Json constructor.
/// </summary>
public class Powerup
{
    int collisionRadius = 10;

    /// <summary>
    /// Getter/Setter the powerup ID
    /// </summary>
    public int power { get; set; }
    /// <summary>
    /// Getter/Setter for the location of the powerup -- of the form, Vector2D.
    /// </summary>
    public Vector2D loc { get; set; }
    /// <summary>
    /// Getter/Setter for whether or not the powerup has been "eaten", true if yes, false if no.
    /// </summary>
    public bool died { get; set; }

    /// <summary>
    /// Parameterized Json constructor for Powerup Class -- used for Json deserialization.
    /// </summary>
    /// <param name="power">unique id of powerup.</param>
    /// <param name="loc">location of powerup (x and y coord.)</param>
    /// <param name="died">Boolean value if powerup has been used already.</param>
    [JsonConstructor]
    public Powerup(int power, Vector2D loc, bool died)
    {
        this.power = power;
        this.loc = loc;
        this.died = died;

    }

    /// <summary>
    /// Collision Radius 
    /// </summary>
    /// <returns></returns>
    public int GetCollisionRadius()
    {
        return collisionRadius;
    }
}
