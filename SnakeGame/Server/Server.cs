using NetworkUtil;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;


namespace SnakeGame;

public class Server
{


    private Dictionary<long, SocketState> clients;
    private World theWorld;

    public delegate void ServerUpdateHandler(IEnumerable<Snake> snake, IEnumerable<Powerup> powerups, IEnumerable<Wall> walls);
    public event ServerUpdateHandler? ServerUpdate;

    //trial

    double segmentLength = 3;
  

    public long msPerFrame = 0;
    public int respawnRate = 0;
    public int size = 0;
    private int maxPlayers = 0;
    private int maxPowerups = 0;
    private int PlayerID = 0;

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
    }

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
                while (watch.ElapsedMilliseconds < msPerFrame)
                { //empty here because we're timing the systems counter per Frame
                }
                moveSnake();
                FPS++;
                watch.Restart();
                Update();
                //  ServerUpdate?.Invoke(theWorld.Players.Values, theWorld.Powerups.Values);

            }
            Console.WriteLine("FPS: " + FPS);
            fpsWatch.Restart();
        }
    }

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
    /// Just removes the newline character
    /// 
    /// </summary>
    /// <param name="message"></param>
    private void ProcessMessage(SocketState state)
    {
        string totalData = state.GetData();

        string[] parts = Regex.Split(totalData, @"(?<=[\n])");

        // Loop until we have processed all messages.
        // We may have received more than one.
        foreach (string p in parts)
        {
            // Ignore empty strings added by the regex splitter
            if (p.Length == 0)
                continue;

            // The regex splitter will include the last string even if it doesn't end with a '\n',
            // So we need to ignore it if this happens. 
            if (p[p.Length - 1] != '\n')
                break;

            //Console.WriteLine("received message from client " + state.ID + ": \"" + p.Substring(0, p.Length - 1) + "\"");

            // Remove it from the SocketState's growable buffer
            state.RemoveData(0, p.Length);

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
        Networking.Send(state.TheSocket, size.ToString() + "\n");
        Console.WriteLine("World Size: " + size.ToString());
        lock (theWorld)
        {
            Console.WriteLine("All walls sent as JSON strings");
            foreach (Wall wall in theWorld.Walls.Values)
            {
                string AsJSON = JsonSerializer.Serialize(wall);

                Console.Write(AsJSON + "\n");
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
            Console.WriteLine("This is what movement contains: " + movement);
            if (movement.Contains("up"))
            {
                Console.WriteLine("Im moving up!");
                Vector2D dir = new Vector2D(0, 1);
                double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                Vector2D newHead = new(oldX, oldY + segmentLength);
                theWorld.Players[(int)state.ID].body.Add(newHead); //append a head
                theWorld.Players[(int)state.ID].body.Remove(theWorld.Players[(int)state.ID].body.First()); //remove tai

                theWorld.Players[(int)state.ID].dir = dir;
                state.RemoveData(0, movement.Length);
            }
            else if (movement.Contains("left"))
            {
                Console.WriteLine("Im moving left!");
                Vector2D dir = new Vector2D(-1, 0);
                double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                Vector2D newHead = new(oldX - segmentLength, oldY);
                theWorld.Players[(int)state.ID].body.Add(newHead); //append a head
                theWorld.Players[(int)state.ID].body.Remove(theWorld.Players[(int)state.ID].body.First()); //remove tail
                theWorld.Players[(int)state.ID].dir = dir;
                state.RemoveData(0, movement.Length);
            }
             if (movement.Contains("down"))
            {
                Console.WriteLine("Im moving down!");
                Vector2D dir = new Vector2D(0, -1);
                double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                Vector2D newHead = new(oldX, oldY - segmentLength);
                theWorld.Players[(int)state.ID].body.Add(newHead); //append a head
                theWorld.Players[(int)state.ID].body.Remove(theWorld.Players[(int)state.ID].body.First()); //remove tai
                theWorld.Players[(int)state.ID].dir = dir;
                state.RemoveData(0, movement.Length);
            }
            else if (movement.Contains("right"))
            {
                Console.WriteLine("Im moving right!");
                Vector2D dir = new Vector2D(1, 0);
                double oldX = theWorld.Players[(int)state.ID].body.Last().GetX();
                double oldY = theWorld.Players[(int)state.ID].body.Last().GetY();
                Vector2D newHead = new(oldX + segmentLength, oldY);
                theWorld.Players[(int)state.ID].body.Add(newHead); //append a head
                theWorld.Players[(int)state.ID].body.Remove(theWorld.Players[(int)state.ID].body.First()); //remove tai

                theWorld.Players[(int)state.ID].dir = dir;
                state.RemoveData(0, movement.Length);
            }
        }

        // 2) ask for more data
        Networking.GetData(state);
    }

    private void moveSnake()
    {
        Vector2D up = new Vector2D(0, 1);
        Vector2D down = new Vector2D(0, -1);
        Vector2D left = new Vector2D(-1, 0);
        Vector2D right = new Vector2D(1, 0);

        foreach (Snake snake in theWorld.Players.Values)
        {
            if (snake.dir == up)
            {
                Vector2D velocityUp = new Vector2D(0, 6);
                for (int i = 0; i < snake.body.Count; i++)
                    snake.body[i] = snake.body[i] + velocityUp;
            }
            else if (snake.dir == down)
            {
                Vector2D velocityDown = new Vector2D(0, -6);
                for (int i = 0; i < snake.body.Count; i++)
                    snake.body[i] = snake.body[i] + velocityDown;
            }
            else if (snake.dir == left)
            {
                Vector2D velocityLeft = new Vector2D(-6, 0);
                for (int i = 0; i < snake.body.Count; i++)
                    snake.body[i] = snake.body[i] + velocityLeft;
            }
            else if (snake.dir == right)
            {
                Vector2D velocityRight = new Vector2D(6, 0);
                for (int i = 0; i < snake.body.Count; i++)
                    snake.body[i] = snake.body[i] + velocityRight;
            }
        }
    }

    /// <summary>
    /// This is the method invoked every iteration through the frame loop. 
    /// Update the world then send it to each client
    /// </summary>
    private void Update()
    {
        lock (theWorld)
        {
            //Check if there is any dead snakes or powerups to remove, then remove them. 
            IEnumerable<int> IDsOfSnakesToRemove = theWorld.Players.Values.Where(x => x.died).Select(x => x.snake);
            foreach (int snakeID in IDsOfSnakesToRemove)
                theWorld.Players.Remove(snakeID);
            IEnumerable<int> IDsOfPowerupsToRemove = theWorld.Powerups.Values.Where(x => x.died).Select(x => x.power);
            foreach (var powerups in IDsOfPowerupsToRemove)
                theWorld.Powerups.Remove(powerups);

            //send snake and powerup data to client 
            foreach (Snake snake in theWorld.Players.Values)
            {
                try
                {
                    string JsonSnake = JsonSerializer.Serialize(snake);
                    // Console.Write(JsonSnake + "\n");
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
                    // Console.Write(JsonPowerup + "\n");
                    SendToAllClients(JsonPowerup);
                }
                catch (JsonException e)
                {

                    Console.WriteLine("Error Parsing Powerup JSON: " + e);
                }
            }

        }
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

    private string JsonWorld()
    {
        //Snakes, powerups, and walls to JSON -- i hardcoded values just testing things out.
        Vector2D direction = new(0, 1);
        Vector2D head = new(1, 4);
        Vector2D tail = new(1, 1);
        List<Vector2D> bodyList = new();
        bodyList.Add(tail);
        bodyList.Add(head);

        List<JsonDocument> worldState = new List<JsonDocument>();
        foreach (var player in theWorld.Players.Values)
        {
            var snake = new
            {
                snake = PlayerID,
                name = "stuart",
                body = bodyList,
                dir = direction,
                score = 0,
                died = false,
                alive = true,
                dc = false,
                join = true
            };
            worldState.Add(JsonDocument.Parse(JsonSerializer.Serialize(snake)));
        }
        foreach (var wall in theWorld.Walls.Values)
        {
            Vector2D start = new(10, 0);
            Vector2D finish = new(10, 2);

            var walls = new
            {
                wall = 1,
                p1 = start,
                p2 = finish
            };
            worldState.Add(JsonDocument.Parse(JsonSerializer.Serialize(walls)));
        }
        foreach (var powerup in theWorld.Powerups.Values)
        {
            Vector2D poweruppoint = new(5, 0);

            var powerups = new
            {
                power = 1,
                loc = poweruppoint,
                died = false
            };
            worldState.Add(JsonDocument.Parse(JsonSerializer.Serialize(powerups)));
        }
        return JsonSerializer.Serialize(worldState.Select(doc => doc.RootElement));
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
    /// Parses XML file and gets settings for the server.
    /// </summary>
    private void ParseSettingsXMLFile()
    {
        // does this need to account for dynamic local paths on a any computer? 
        //string relativePath = "C:\\Users\\parke\\source\\repos\\game-bytebuddies_game\\SnakeGame\\settings.xml";
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

        // Select the XML node using the tag name
        XmlNode respawnRateNode = xmlDoc.SelectSingleNode(respawnRateTag)!;
        XmlNode msPerFrameNode = xmlDoc.SelectSingleNode(msPerFrameTag)!;
        XmlNode universeSizeNode = xmlDoc.SelectSingleNode(universeSizeTag)!;
        XmlNode powerupCapNode = xmlDoc.SelectSingleNode(powerupCapTag)!;
        XmlNode playerCapNode = xmlDoc.SelectSingleNode(playerCapTag)!;

        if (respawnRateNode != null && int.TryParse(respawnRateNode.InnerText, out int respawnRate))
        {
            Console.WriteLine($"Parsed Respawn Rate: {respawnRate}");
            this.respawnRate = respawnRate;
        }
        if (msPerFrameNode != null && int.TryParse(msPerFrameNode.InnerText, out int msPerFrame))
        {
            Console.WriteLine($"Parsed MSPerFrame: {msPerFrame}");
            this.msPerFrame = msPerFrame;
        }
        if (universeSizeNode != null && int.TryParse(universeSizeNode.InnerText, out int universeSize))
        {
            Console.WriteLine($"Parsed UniverseSize: {universeSize}");
            size = universeSize;
        }
        if (powerupCapNode != null && int.TryParse(powerupCapNode.InnerText, out int powerupCap))
        {
            Console.WriteLine($"Parsed PowerupCap: {powerupCap}");
            maxPowerups = powerupCap;
        }
        if (playerCapNode != null && int.TryParse(playerCapNode.InnerText, out int playerCap))
        {
            Console.WriteLine($"Parsed PlayerCap: {playerCap}");
            maxPlayers = playerCap;
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
                        //  Console.WriteLine("Found the element");
                        Wall p = (Wall)wallSer.ReadObject(reader)!;
                        //  Console.WriteLine("{0} {1}    id:{2}",
                        //      p.p1, p.p2, p.wall);

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





