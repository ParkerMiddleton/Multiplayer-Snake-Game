﻿using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace SnakeGame;

/// <summary>
/// <Author>Abbey Lasater</Author>
/// <Author>Parker Middleton</Author>
/// <Date>November 24th, 2023</Date>
/// The Walls class represents the Walls in the world.
/// </summary>
[DataContract(Name = "Wall", Namespace = "")]
public class Wall
{
    /// <summary>
    /// Getter/setter method for wall ID.
    /// </summary>
    [DataMember(Name = "ID")]
    public int wall { get; set; }

    /// <summary>
    /// Getter/setter method for starting point of wall
    /// </summary>
    [DataMember]
    public Vector2D p1 { get; set; }

    /// <summary>
    /// Getter/setter method for end point of wall
    /// </summary>
    [DataMember]
    public Vector2D p2 { get; set; }

    /// <summary>
    /// Parameterized Json constructor for Wall Class -- used for Json deserialization.
    /// </summary>
    /// <param name="wall"> int representing the walls unique ID</param>
    /// <param name="p1">starting point of wall as a vector2D</param>
    /// <param name="p2"> ending point of wall as a vector2D</param>
    [JsonConstructor]
    public Wall(int wall, Vector2D p1, Vector2D p2)
    {
        this.wall = wall;
        this.p1 = p1;
        this.p2 = p2;
    }
}
