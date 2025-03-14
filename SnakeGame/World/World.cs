﻿namespace SnakeGame;

///<summary>
///<Author>Abbey Lasater</Author>
///<Author>Parker Middleton</Author>
///<Date>November 24th, 2023</Date>
///The World class represents the game world.
///<summary>
public class World
{
    

    /// <summary>
    /// Maps snake IDs to Snake objects.
    /// </summary>
    public Dictionary<int, Snake> Players { get; set; }
    /// <summary>
    ///  Maps wall IDs to Wall objects.
    /// </summary>
    public Dictionary<int, Wall> Walls { get; set; }
    /// <summary>
    /// Maps powerup IDs to Powerup objects.
    /// </summary>
    public Dictionary<int, Powerup> Powerups { get; set; }

    /// <summary>
    /// Getter/Setter method for the size of the world.
    /// </summary>
    public int size { get; set; }

    /// <summary>
    /// Parameterized contructor for World class.
    /// </summary>
    /// <param name="_size">The size of the world. As in the size of the width and length. </param>
    public World(int _size)
    {
        Players = new Dictionary<int, Snake>();
        Walls = new Dictionary<int, Wall>();
        Powerups = new Dictionary<int, Powerup>();
        this.size = _size;
    }
}

