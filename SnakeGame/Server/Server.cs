using Microsoft.VisualBasic;
using NetworkUtil;
using System.Reflection;
using System.Drawing;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace SnakeGame;

/// <summary>
/// TODO:
/// Player Vs Player Collision 
/// Self Collision
/// Zig Zag Collision
/// More work on respawn player method 
/// Delays 
///  -> Powerup Respawn timer 
///  -> Player Death (How long the player sits on a death animation)
///  
/// Wrap Around 
/// README 
/// </summary>
public class Server
{
    // always lock before modifying or iterating through any of these
    private readonly Dictionary<long, SocketState> clients;
    private readonly World theWorld;

    // Setting values treated as constants
    private long MS_Per_Frame = 0;
    private int Respawn_Rate = 0;
    private double Snake_Speed = 6;
    private int World_Size = 0;
    private int Max_Players = 0;
    private int Max_Powerups = 0;
    private int Powerup_Respawn_Delay = 0;
    private int Snake_Starting_Length = 0;
    private int Snake_Growth_Rate = 0;
    private int Powerup_Eaten_Delay = 0;

    //Basic Movement Vectors
    private readonly Dictionary<int, Vector2D> Directions = new();

    // individual directions
    private readonly Vector2D UP = new Vector2D(0, -1);
    private readonly Vector2D DOWN = new Vector2D(0, 1);
    private readonly Vector2D LEFT = new Vector2D(-1, 0);
    private readonly Vector2D RIGHT = new Vector2D(1, 0);


    /// <summary>
    /// Starts the server, loads xml then continuously outputs frames per second. 
    /// </summary>
    /// <param name="args">Arguments</param>
    static void Main(string[] args)
    {
        Server server = new Server(0);
        server.ParseSettingsXMLFile();
        server.StartServer();

        server.StartFPSCounter();
    }

    /// <summary>
    /// Constructs a server object
    /// </summary>
    /// <param name="s">size of the world</param>
    public Server(int s)
    {
        theWorld = new World(s);
        clients = new Dictionary<long, SocketState>();
        Directions.Add(0, UP);
        Directions.Add(1, DOWN);
        Directions.Add(2, LEFT);
        Directions.Add(3, RIGHT);

    }

    /////////////////////////////////////////////////////////////////////////////////
    ///// NETWORKING ORIENTED METHODS
    ///////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Starts the server and beginins listening for new TCP connections
    /// </summary>
    public void StartServer()
    {
        Networking.StartServer(NewClientConnected, 11000);
        Console.WriteLine("Server is running");
    }

    /// <summary>
    /// This is a delegate callback passed to the networking class to handle a new client connecting. 
    /// Change the callback for the socketState to a new method that recieves the player's name, then ask for data. 
    /// </summary>
    /// <param name="state">Curent state of a client's socket</param>
    private void NewClientConnected(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            RemoveClient(state);
            return;
        }

        // 1)Change the callback for  the SocketState to a new method that recieves the player's name
        state.OnNetworkAction = RecievePlayerName;

        // 2)Ask for Data
        Networking.GetData(state);
    }

    /// <summary>
    /// This is a delegate that implements the server's part of the initial handshake. 
    /// 
    /// Make a new Snake with the given name and unique ID (Recommended using the SocketState'sID). 
    /// Then change the callback to a method that handles command requests from the client. 
    /// Then send the startup info to the client. 
    /// Then add the client's socket to a list of all clients. 
    /// Then ask for data. 
    /// 
    /// Note: it is important that the server sends the startup infor before adding the client to the list of all clients. This 
    /// guarentees that the startup info is sent before any world info. Remember that the server is running a loop on a separate thread 
    /// that may send world info to the list of clients at any time. 
    /// </summary
    /// <param name="state">Curent state of a client's socket</param>
    private void RecievePlayerName(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            RemoveClient(state);
            return;
        }

        // ProcessMessage(state);

        string playerName = state.GetData().Trim();
        // 1) Make a new Snake with the given name and unique ID (Recommended using the SocketState'sID). 
        Snake snake = new((int)state.ID, playerName);

        //Display connection status
        Console.WriteLine("Client (" + state.ID + ") " + playerName + " Connected");

        // 2) Change the callback to a method that handles command requests from the client. 
        state.OnNetworkAction = HandleSnakeMovementCommands; // After this is called, all Networking.Send calls from the client will be move commands

        // 3) Send the startup info to the client.
        SendStartUpJson(state, snake);

        // 4) Add the client's socket to a list of all clients.
        lock (clients)
        {
            clients[state.ID] = state;
        }

        // 5) Ask for Data
        Networking.GetData(state);
    }

    /// <summary>
    /// Sends data to every active client in a thread safe way. 
    /// </summary>
    /// <param name="data">Some text to be sent</param>
    private void SendToAllClients(string data)
    {
        lock (clients)
        {
            foreach (SocketState client in clients.Values)
            {
                Networking.Send(client.TheSocket, data + '\n');
            }
        }
    }

    /// <summary>
    /// Removes the client (in a thread safe way) from the clients socket dictionary
    /// </summary>
    /// <param name="id">SocketState ID</param>
    private void RemoveClient(SocketState state)
    {
        lock (theWorld)
        {
            Console.WriteLine("Client (" + state.ID + ")" + theWorld.Players[(int)state.ID].name + " Disconnected");
            theWorld.Players[(int)state.ID].died = true;
            theWorld.Players[(int)state.ID].alive = false;
            theWorld.Players[(int)state.ID].dc = true;


        }
        lock (clients)
        {
            clients.Remove(state.ID);
        }
    }

    /// <summary>
    /// Private Helper function for sending initial connection JSON data to the client
    /// </summary>
    /// <param name="state">Client connection</param>
    /// <param name="snake">Client snake</param>
    private void SendStartUpJson(SocketState state, Snake snake)
    {
        // send player ID, world size, all walls
        Networking.Send(state.TheSocket, snake.snake + "\n");
        Console.WriteLine("Snake ID: " + snake.snake);
        Networking.Send(state.TheSocket, World_Size.ToString() + "\n");
        Console.WriteLine("World Size: " + World_Size.ToString());
        lock (theWorld)
        {
            // Console.WriteLine("All walls sent as JSON strings");
            foreach (Wall wall in theWorld.Walls.Values)
            {
                string AsJSON = JsonSerializer.Serialize(wall);

                // Console.Write(AsJSON + "\n");
                Networking.Send(state.TheSocket, AsJSON + '\n');
            }
            theWorld.Players.Add(snake.snake, snake);
        }
    }

    /// <summary>
    /// This is a delegate for processing client direction commands. 
    /// 
    /// Process the command then ask for more data. 
    /// 
    /// Note: In order to know which client the request came from , the SocketState must contain the player's ID.
    /// This is what the Id is for in the SocketState class. 
    /// </summary>
    /// <param name="state">Curent state of a client's socket</param>
    private void HandleSnakeMovementCommands(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            RemoveClient(state);
            return;
        }

        lock (theWorld)
        {
            // 1) Process the command
            string movement = state.GetData();
            if (movement.Contains("up"))
            {
                if (!theWorld.Players[(int)state.ID].dir.Equals(UP)
                    && !theWorld.Players[(int)state.ID].dir.Equals(DOWN))
                {
                    double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                    double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                    Vector2D newHead = new(oldX, oldY);
                    theWorld.Players[(int)state.ID].body.Add(newHead);
                    theWorld.Players[(int)state.ID].dir = UP;

                }
                state.RemoveData(0, movement.Length);
            }
            else if (movement.Contains("left"))
            {
                if (!theWorld.Players[(int)state.ID].dir.Equals(LEFT)
                   && !theWorld.Players[(int)state.ID].dir.Equals(RIGHT))
                {
                    double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                    double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                    Vector2D newHead = new(oldX, oldY);
                    theWorld.Players[(int)state.ID].body.Add(newHead);
                    theWorld.Players[(int)state.ID].dir = LEFT;

                }
                state.RemoveData(0, movement.Length);
            }
            else if (movement.Contains("down"))
            {
                if (!theWorld.Players[(int)state.ID].dir.Equals(DOWN)
                   && !theWorld.Players[(int)state.ID].dir.Equals(UP))
                {
                    double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                    double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                    Vector2D newHead = new(oldX, oldY);
                    theWorld.Players[(int)state.ID].body.Add(newHead);
                    theWorld.Players[(int)state.ID].dir = DOWN;

                }
                state.RemoveData(0, movement.Length);
            }
            else if (movement.Contains("right"))
            {
                if (!theWorld.Players[(int)state.ID].dir.Equals(RIGHT)
                 && !theWorld.Players[(int)state.ID].dir.Equals(LEFT))
                {
                    double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                    double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                    Vector2D newHead = new(oldX, oldY);
                    theWorld.Players[(int)state.ID].body.Add(newHead);
                    theWorld.Players[(int)state.ID].dir = RIGHT;

                }
                state.RemoveData(0, movement.Length);
            }
        }

        // 2) ask for more data
        Networking.GetData(state);
    }

    /////////////////////////////////////////////////////////////////////////////////////////
    /// WORLD STATE ORIENTED METHODS
    ////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Starts the FPS counter and continues the loop on forever. 
    /// </summary>
    public void StartFPSCounter()
    {
        System.Diagnostics.Stopwatch fpsWatch = new System.Diagnostics.Stopwatch();
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

        while (true)
        {
            int FPS = 0;
            fpsWatch.Start();
            while (fpsWatch.ElapsedMilliseconds < 1000)
            {
                watch.Start();
                // wait until the next frame
                while (watch.ElapsedMilliseconds < MS_Per_Frame)
                { //empty here because we're timing the systems counter per Frame
                }
                //In place to make sure that a snake gets the desired amount of frames to grow when eating a powerup
                if (Powerup_Eaten_Delay > 0)
                    Powerup_Eaten_Delay--;
                FPS++;
                watch.Restart();

                UpdatePlayerAndPowerupCounts(); // done

                UpdateSnakeMovement(); // done

                CheckForCollisions();

                //WrapAround();
                CheckForPlayerRespawns();

                MakeSurePowerupsAreAtMaximum();

            }
            Console.WriteLine("FPS: " + FPS);
            fpsWatch.Restart();
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////
    /// SNAKE MOVEMENT ORIENTED METHODS
    /////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Keeps the snake's head moving and keeps the tail following the second to last snake segment
    /// provided that the snakes length is not 2 segments long
    /// </summary>
    private void UpdateSnakeMovement()
    {
        lock (theWorld)
        {
            foreach (Snake snake in theWorld.Players.Values)
            {
                //Compute the velocity of the snake's head based on the player's updated movement commmand
                Vector2D velocityVector = new();

                // Add velocity to the head's position
                if (snake.dir.Equals(UP))
                {
                    velocityVector = new Vector2D(0, -Snake_Speed);
                    snake.body[snake.body.Count - 1] += velocityVector;
                }
                else if (snake.dir.Equals(DOWN))
                {
                    velocityVector = new Vector2D(0, Snake_Speed);
                    snake.body[snake.body.Count - 1] += velocityVector;
                }
                else if (snake.dir.Equals(LEFT))
                {
                    velocityVector = new Vector2D(-Snake_Speed, 0);
                    snake.body[snake.body.Count - 1] += velocityVector;
                }
                else if (snake.dir.Equals(RIGHT))
                {
                    velocityVector = new Vector2D(Snake_Speed, 0);
                    snake.body[snake.body.Count - 1] += velocityVector;
                }

                //move the tail vertext by its velocity
                //The tail's velocity is towards the next vertex in the body with a speed equal to the snake's speed

                // if a snake has eaten a powerup, then the tail wont grow, making it longer.
                if (Powerup_Eaten_Delay == 0)
                {
                    // if there is only two segments in the list, their velocites are the same, therefore no action necessary 
                    if (snake.body.Count > 2)
                    {
                        velocityVector = GetSnakesTailDirectionVector(snake);
                        snake.body[0] += velocityVector;

                        // if the snake tail and the segment before it have the same position then remove the tail 
                        if (snake.body[0].Equals(snake.body[1]))
                        {
                            snake.body.RemoveAt(0);
                        }
                    }
                    else
                    {
                        snake.body[0] += velocityVector;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the direction that the tail should go based on the previous body segment in the list. 
    /// </summary>
    /// <returns> Direction the Snake's Tail should travel</returns>
    private Vector2D GetSnakesTailDirectionVector(Snake s)
    {

        Vector2D secondToLastVector = new Vector2D(s.body[1]);
        Vector2D LastVector = new Vector2D(s.body[0]);
        float angle = Vector2D.AngleBetweenPoints(secondToLastVector, LastVector);
        switch (angle)
        {
            //Negative X
            case -90: return new Vector2D(-Snake_Speed, 0);
            //Positive Y
            case 0: return new Vector2D(0, -Snake_Speed);
            //Positive X
            case 90: return new Vector2D(Snake_Speed, 0);
            //Negative Y
            case 180: return new Vector2D(0, Snake_Speed);
        }
        return new Vector2D();
    }

    /// <summary>
    /// Determines if a wrap around has occoured and adjusts the snake accordingly
    /// NOTE: Not fully implemented yet
    /// </summary>
    private void WrapAround()
    {
        foreach (Snake snake in theWorld.Players.Values)
        {
            double range = World_Size / 2;
            List<Vector2D> updatedBody = new List<Vector2D>();
            foreach (Vector2D segment in snake.body)
            {
                double newX = segment.GetX();
                double newY = segment.GetY();

                if (segment.GetX() > range)
                {
                    newX = -range;
                }
                else if (segment.GetX() < -range)
                {
                    newX = range;
                }

                if (segment.GetY() > range)
                {
                    newY = -range;
                }
                else if (segment.GetY() < -range)
                {
                    newY = range;
                }

                updatedBody.Add(new Vector2D(newX, newY));
            }
            snake.body = updatedBody;
        }
    }


    /////////////////////////////////////////////////////////////////////////////////////
    /// COLLISION ORIENTED METHODS 
    /////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Checks for any kind of collision
    /// </summary>
    private void CheckForCollisions()
    {
        lock (theWorld)
        {
            // snakes are the only objects that are capable of inducing a collision, therefore iterate through them
            foreach (Snake snake in theWorld.Players.Values)
            {
                //Snake Collides with Wall  || Snake Collides with other player

                if (CheckForWallCollision(snake.body.Last(), 0) || CheckForPlayerVsPlayerCollision(snake)) // || CheckForSelfCollision(
                {
                    snake.died = true;
                    snake.alive = false;
                }

                //Snake Collides With Powerup 
                if (CheckForPowerupCollision(snake))
                {
                    snake.score++;
                }
            }
        }
    }

    /// <summary>
    /// Checks for a snake and powerup collision on a given frame
    /// </summary>
    /// <param name="snake">A snake present in the world</param>
    /// <returns>boolean</returns>
    private bool CheckForPowerupCollision(Snake snake)
    {
        lock (theWorld)
        {
            foreach (Powerup power in theWorld.Powerups.Values)
            {
                int powerupCollisionRadius = power.GetCollisionRadius();
                Vector2D powerupLocation = power.loc;
                int snakeHeadCollisionRadius = snake.GetCollisonRadius();
                Vector2D snakeHeadLocation = snake.body.Last();
                double distance = Vector2D.DistanceBetweenTwoVectors(powerupLocation, snakeHeadLocation);
                int totalRadius = powerupCollisionRadius + snakeHeadCollisionRadius;

                if (distance <= totalRadius)
                {
                    power.died = true;
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Checks for a Wall collisions given a specific vector with a given buffer. 
    /// Users of this method should pass zero as the buffer when referring to a direct collision, but should 
    /// pass a buffer of variable size if an additional boundary surrounding the wall should be counted as a collision. 
    /// </summary>
    /// <param name="obj">A vector that is being tested for collision</param>
    /// <param name="buffer">extra range that should be counted as a collision barrier</param>
    /// <returns>Boolean</returns>
    private bool CheckForWallCollision(Vector2D obj, int buffer)
    {
        lock (theWorld)
        {
            foreach (Wall wall in theWorld.Walls.Values)
            {
                double wallStartX = wall.p1.GetX() - 25 - buffer;
                double wallEndX = wall.p2.GetX() + 25 + buffer;
                double wallStartY = wall.p1.GetY() - 25 - buffer;
                double wallEndY = wall.p2.GetY() + 25 + buffer;

                double oppWallStartX = wall.p1.GetX() + 25 + buffer;
                double oppWallEndX = wall.p2.GetX() - 25 - buffer;
                double oppWallStartY = wall.p1.GetY() + 25 + buffer;
                double oppWallEndY = wall.p2.GetY() - 25 - buffer;

                if ((obj.GetX() >= wallStartX && obj.GetX() <= wallEndX &&
                     obj.GetY() >= wallStartY && obj.GetY() <= wallEndY) ||
                    (obj.GetX() <= oppWallStartX && obj.GetX() >= oppWallEndX &&
                     obj.GetY() <= oppWallStartY && obj.GetY() >= oppWallEndY))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Determines if a snake has hit another snake on a given frame
    /// </summary>
    /// <param name="snake"></param>
    /// <returns></returns>
    private bool CheckForPlayerVsPlayerCollision(Snake snake) //heavent checked this and need to check if head collides with any point betweeen vectors of othersnake body
    {
        bool snakeCollision = false;
        lock (theWorld)
        {
            // iterate through all current snakes
            foreach (Snake otherSnake in theWorld.Players.Values)
            {
                if (!snake.snake.Equals(otherSnake.snake))
                {
                    for (int i = 1; i < otherSnake.body.Count; i++)
                    {
                        if (snake.body.Last().GetX() >= otherSnake.body[i].GetX() && snake.body.Last().GetX() <= otherSnake.body[i + 1].GetX() &&
                            snake.body.Last().GetY() >= otherSnake.body[i].GetY() && snake.body.Last().GetY() <= otherSnake.body[i + 1].GetY())
                        {
                            snakeCollision = true;
                        }
                        snakeCollision = false;
                    }
                }
            }
        }
        return snakeCollision;


                //// make sure were not comparing the current snake to itself
                //if (!snake.Equals(otherSnake)) // Note: added a .Equals method for snake model class
                //{
                //    // head on head collision
                //    Vector2D currentHeadLocation = snake.body.Last();
                //    int currentCollisionRadius = snake.GetCollisonRadius();
                //    Vector2D otherHeadLocation = otherSnake.body.Last();
                //    int otherCollisionRadius = snake.GetCollisonRadius();
                //    double distance = Vector2D.DistanceBetweenTwoVectors(currentHeadLocation, otherHeadLocation);
                //    int totalRadius = currentCollisionRadius + otherCollisionRadius;

                //    // if snakes collide head on, it is decided at random who will die.
                //    if (distance <= totalRadius)
                //    {
                //        Random dieDecider = new();
                //        if (dieDecider.Next(0, 1) == 1)
                //        {
                //            snake.died = true;
                //            snake.alive = false;
                //            return true;
                //        }
                //        else
                //        {
                //            otherSnake.died = true;
                //            otherSnake.alive = false;
                //            return true;
                //        }
                //    }

                //    // head on opposing snakes body collision
                //    // count backwards from the head until i == 1 so zero isnt hit
                //    for (int i = otherSnake.body.Count - 1; i > 1; i--)
                //    {
                //        // Get the direction that the two vectors are facing
                //        double angle = Vector2D.AngleBetweenPoints(otherSnake.body[i], otherSnake.body[i - 1]);

                //        //Console.WriteLine(angle + "");
                //        // segment is Vertical
                //        // just trying to get one segment to collide first before progressing. 
                //        if (angle == 180)
                //        {
                //            double upperY = otherSnake.body[i].GetY();
                //            double upperX = otherSnake.body[i].GetX();
                //            double lowerY = otherSnake.body[i - 1].GetY();
                //            double lowerX = otherSnake.body[i - 1].GetX();

                //            if (snake.body.Last().GetY() <= upperY && snake.body.Last().GetY() >= lowerY &&
                //                snake.body.Last().GetX() <= upperX && snake.body.Last().GetX() >= lowerX)
                //            {
                //                snake.died = true;
                //                snake.alive = false;
                //                return true;
                //            }
                //        }

                        //// segment is horizontal
                        //if (angle == 90 )
                        //{
                        //    double upperY = otherSnake.body[i].GetX();
                        //    double lowerY = otherSnake.body[i - 1].GetX();
                        //    if (snake.body.Last().GetX() <= upperY && snake.body.Last().GetX() >= lowerY)
                        //    {
                        //        snake.died = true;
                        //        snake.alive = false;
                        //        return true;
                        //    }
                        //}

                        //// segment is Vertical
                        //if (angle == 180)
                        //{
                        //    double upperY = otherSnake.body[i - 1].GetY();
                        //    double lowerY = otherSnake.body[i].GetY();
                        //    if (snake.body.Last().GetY() >= upperY && snake.body.Last().GetY() <= lowerY)
                        //    {
                        //        snake.died = true;
                        //        snake.alive = false;
                        //        return true;
                        //    }
                        //}

                        //// segment is horizontal
                        //if (angle == -90)
                        //{
                        //    double upperY = otherSnake.body[i - 1].GetX();
                        //    double lowerY = otherSnake.body[i].GetX();
                        //    if (snake.body.Last().GetX() >= upperY && snake.body.Last().GetX() <= lowerY)
                        //    {
                        //        snake.died = true;
                        //        snake.alive = false;
                        //        return true;
                        //    }
        //                //}
        //            }

        //        }
        //    }
        //    return false;
        //}
    }


    /// <summary>
    /// Determines if the snake has hit itself on a given frame
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    private bool CheckForSelfColision()
    {
        throw new NotImplementedException();
    }

    /////////////////////////////////////////////////////////////////////////////////////
    /// RESPAWN ORIENTED METHODS
    ////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Checks to see if there are any currently dead players that need to be respawned
    /// </summary>
    private void CheckForPlayerRespawns()
    {
        lock (theWorld)
        {
            foreach (Snake snake in theWorld.Players.Values)
            {
                if (snake.died)
                {
                    Vector2D headDirection = new();
                    List<Vector2D> body = new List<Vector2D>();

                    bool isInvalidLocation = false;
                    do
                    {
                        // randomize the direction 
                        Random directionRandomizer = new Random();
                        int directionPicker = directionRandomizer.Next(0, 3);
                        headDirection = Directions.GetValueOrDefault(directionPicker)!;

                        // randomize the spawn location x and y 
                        Random locationRandomizer = new Random();

                        // randomize the location but make sure that the snake never spawns off a ledge, subtract its starting length to prevent
                        int Xcoord = locationRandomizer.Next((-World_Size / 2) + Snake_Starting_Length, (World_Size / 2) - Snake_Starting_Length);
                        int Ycoord = locationRandomizer.Next((-World_Size / 2) + Snake_Starting_Length, (World_Size / 2) - Snake_Starting_Length);
                        Vector2D headLocation = new Vector2D(Xcoord, Ycoord);

                        // derive the tails location based on those 2 variables
                        Vector2D tailLocation = headLocation - (headDirection * (double)Snake_Starting_Length);
                        body = new List<Vector2D>() { tailLocation, headLocation };

                        //make sure head and tail arent overlapping a wall segment, pass the desired Snake_Starting_Length to make sure that
                        // there is never any objects in between the snake when spawning.
                        foreach (Vector2D segment in body)
                        {
                            isInvalidLocation = CheckForWallCollision(segment, Snake_Starting_Length);
                            if (isInvalidLocation)
                            {
                                break;
                            }
                        }
                        // while true indicating that when its not true, the loop breaks
                    } while (isInvalidLocation);

                    snake.dir = headDirection;
                    snake.body = body;
                    snake.alive = true;
                    snake.died = false;
                    snake.score = 0;
                }
            }
        }
    }

    /// <summary>
    /// Consistantly maintians a XML desired amount of powerups on the board.
    /// </summary>
    private void MakeSurePowerupsAreAtMaximum()
    {
        Random rand = new Random(); // There has to be a better way to do this. This still has the chance to throw an exception
        int ID = rand.Next();
        if (theWorld.Powerups.Count < Max_Powerups)
        {
            for (int i = 0; i < Max_Powerups - theWorld.Powerups.Count; i++)
            {
                ID += 12;
                Powerup powerup = new Powerup(ID, GeneratePowerupSpawnLocation(), false);
                theWorld.Powerups.Add(ID, powerup);
            }
        }
    }

    /// <summary>
    /// Provides a Vector that doesnt collide with exisiting walls or snakes in the world. 
    /// X and Y values are randomly assigned but within the bounds of the world size. 
    /// </summary>
    /// <returns></returns>
    private Vector2D GeneratePowerupSpawnLocation()
    {
        Random rand = new();
        Vector2D location;
        bool isInvalidSpawn;
        do
        {
            int Xcoord = rand.Next(-World_Size / 2, World_Size / 2);
            int Ycoord = rand.Next(-World_Size / 2, World_Size / 2);
            location = new Vector2D(Xcoord, Ycoord);

            // see if the desired location is valid with an addtional 100 pixel buffer that 
            // makes sure powerups dont spawn close to walls
            isInvalidSpawn = CheckForWallCollision(location, 100);

        } while (isInvalidSpawn);

        return location;
    }

    /// <summary>
    /// make sure the player doesnt spawn directly on top of a powerup
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public bool CheckForPowerupCollisionOnPlayerSpawn(Vector2D obj)
    {
        return true;
    }

    /// <summary>
    /// This is the method invoked every iteration through the frame loop. 
    /// Update the world then send it to each client
    /// Checks if players and powerups have died, then removes them from their repsective lists before sending that data to the client
    /// </summary>
    private void UpdatePlayerAndPowerupCounts()
    {
        lock (theWorld)
        {
            //send snake and powerup data to client 
            foreach (Snake snake in theWorld.Players.Values)
            {
                try
                {
                    string JsonSnake = JsonSerializer.Serialize(snake);
                    //Console.Write(JsonSnake + "\n");
                    SendToAllClients(JsonSnake);
                }
                catch (JsonException e)
                {

                    Console.WriteLine("Error Parsing Snake JSON: " + e);
                }

            }

            foreach (Powerup powerup in theWorld.Powerups.Values)
            {
                try
                {
                    string JsonPowerup = JsonSerializer.Serialize(powerup);
                    //Console.Write(JsonPowerup + "\n");
                    SendToAllClients(JsonPowerup);

                }
                catch (JsonException e)
                {
                    Console.WriteLine("Error Parsing Powerup JSON: " + e);
                }
            }


            //Check if there is any dead snakes or powerups to remove, then remove them. 
            IEnumerable<int> IDsOfSnakesToRemove = theWorld.Players.Values.Where(x => x.died).Select(x => x.snake);
            foreach (int snakeID in IDsOfSnakesToRemove)
                theWorld.Players.Remove(snakeID);

            IEnumerable<int> IDsOfPowerupsToRemove = theWorld.Powerups.Values.Where(x => x.died).Select(x => x.power);
            foreach (var powerups in IDsOfPowerupsToRemove)
                theWorld.Powerups.Remove(powerups);




        }
    }

    //////////////////////////////////////////////////////////////////////////
    /// GENERAL HELPERS 
    //////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Retrieves a direction vector based off of a cardinal angle direction
    /// </summary>
    /// <param name="angle">Angle between two vectors</param>
    /// <returns>Vector2D</returns>
    private Vector2D GetDirectionVectorFromAngle(double angle)
    {
        if (angle == 0)
            return UP;
        if (angle == 90)
            return RIGHT;
        if (angle == -90)
            return LEFT;
        return DOWN;
    }


    //////////////////////////////////////////////////////////////////////////
    /// XML ORIENTED METHODS
    //////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Parses XML file and gets settings for the server.
    /// </summary>
    private void ParseSettingsXMLFile()
    {
        string relativePath = "settings.xml";

        //First method of parsing XML is used here because the data that is parsed are just primitive types. 
        // Load the XML document from a file
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(relativePath);

        // Specify the name of the custom tag
        string respawnRateTag = "//RespawnRate";
        string msPerFrameTag = "//MSPerFrame";
        string universeSizeTag = "//UniverseSize";
        string powerupCapTag = "//PowerupCap";
        string playerCapTag = "//PlayerCap";
        string snakeSpeedTag = "//SnakeSpeed";
        string snakeStartingLengthTag = "//SnakeStartingLength";
        string snakeGrowthRateTag = "//SnakeGrowthRate";
        string powerupRespawnDelayTag = "//PowerupRespawnDelay";

        // Select the XML node using the tag name
        XmlNode respawnRateNode = xmlDoc.SelectSingleNode(respawnRateTag)!;
        XmlNode msPerFrameNode = xmlDoc.SelectSingleNode(msPerFrameTag)!;
        XmlNode universeSizeNode = xmlDoc.SelectSingleNode(universeSizeTag)!;
        XmlNode powerupCapNode = xmlDoc.SelectSingleNode(powerupCapTag)!;
        XmlNode playerCapNode = xmlDoc.SelectSingleNode(playerCapTag)!;
        XmlNode snakeSpeedNode = xmlDoc.SelectSingleNode(snakeSpeedTag)!;
        XmlNode snakeStartingLengthNode = xmlDoc.SelectSingleNode(snakeStartingLengthTag)!;
        XmlNode snakeGrowthRateNode = xmlDoc.SelectSingleNode(snakeGrowthRateTag)!;
        XmlNode powerupRespawnDelayNode = xmlDoc.SelectSingleNode(powerupRespawnDelayTag)!;


        if (respawnRateNode != null && int.TryParse(respawnRateNode.InnerText, out int respawnRate))
        {
            Console.WriteLine($"Parsed CheckForPlayerRespawns Rate: {respawnRate}");
            this.Respawn_Rate = respawnRate;
        }
        if (msPerFrameNode != null && int.TryParse(msPerFrameNode.InnerText, out int msPerFrame))
        {
            Console.WriteLine($"Parsed MSPerFrame: {msPerFrame}");
            this.MS_Per_Frame = msPerFrame;
        }
        if (universeSizeNode != null && int.TryParse(universeSizeNode.InnerText, out int universeSize))
        {
            Console.WriteLine($"Parsed UniverseSize: {universeSize}");
            World_Size = universeSize;
        }
        if (powerupCapNode != null && int.TryParse(powerupCapNode.InnerText, out int powerupCap))
        {
            Console.WriteLine($"Parsed PowerupCap: {powerupCap}");
            Max_Powerups = powerupCap;
        }
        if (playerCapNode != null && int.TryParse(playerCapNode.InnerText, out int playerCap))
        {
            Console.WriteLine($"Parsed PlayerCap: {playerCap}");
            Max_Players = playerCap;
        }
        if (snakeSpeedNode != null && int.TryParse(snakeSpeedNode.InnerText, out int snakeSpeed))
        {
            Console.WriteLine($"Parsed Snake Speed: {snakeSpeed}");
            Snake_Speed = snakeSpeed;
        }
        if (snakeStartingLengthNode != null && int.TryParse(snakeStartingLengthNode.InnerText, out int snakeStartingLength))
        {
            Console.WriteLine($"Parsed Snake Starting Length: {snakeStartingLength}");
            Snake_Starting_Length = snakeStartingLength;
        }
        if (snakeGrowthRateNode != null && int.TryParse(snakeGrowthRateNode.InnerText, out int snakeGrowthRate))
        {
            Console.WriteLine($"Parsed Snake Growth Rate: {snakeGrowthRate}");
            Snake_Growth_Rate = snakeGrowthRate;
        }
        if (powerupRespawnDelayNode != null && int.TryParse(powerupRespawnDelayNode.InnerText, out int powerupRespawnDelay))
        {
            Console.WriteLine($"Parsed Powerup Respawn Delay: {powerupRespawnDelay}");
            Powerup_Respawn_Delay = powerupRespawnDelay;
        }


        //Parsing walls from file 
        //Different method here because Walls are custom objects
        DataContractSerializer wallSer = new DataContractSerializer(typeof(Wall));
        FileStream fs = new FileStream(relativePath, FileMode.Open);
        XmlDictionaryReader reader =
        XmlDictionaryReader.CreateTextReader(fs, new XmlDictionaryReaderQuotas());
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    if (wallSer.IsStartObject(reader))
                    {
                        Wall p = (Wall)wallSer.ReadObject(reader)!;
                        lock (theWorld)
                        {
                            theWorld.Walls.Add(p.wall, p);
                        }

                    }
                    break;
            }
        }
    }

}





