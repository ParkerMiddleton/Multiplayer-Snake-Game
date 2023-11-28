using NetworkUtil;
using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;


namespace SnakeGame;

internal class Server
{
    private Dictionary<long, SocketState> clients;
    private World theWorld;
    private Random rand = new();



    public delegate void ServerUpdateHandler(IEnumerable<Snake> snake, IEnumerable<Powerup> powerups, IEnumerable<Wall> walls);
    public event ServerUpdateHandler? ServerUpdate;

    private List<Wall> wallsFromXML;


    private long msPerFrame = 24;
    private int maxPlayers = 50;
    private int maxPowerups = 50;
    private int size;

    private int PlayerID = 0;
    private string Name;
    private int nextPowID = 0;
    private int nextWallID = 0;

    static void Main(string[] args)
    {
        Server server = new Server(900);
        server.StartServer();



        Console.Read(); // this makes the server close when you enter anything with the keyboard. 
        // is there a better way to keep the server up? 
    }

    public Server(int s)
    {
        ParseSettingsXMLFile();
        size = s;
        theWorld = new World(size);
        clients = new Dictionary<long, SocketState>();
    }

    public void Run()
    {
        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
        watch.Start();
        Console.Write(watch.ElapsedMilliseconds);
        while (true)
        {
            while (watch.ElapsedMilliseconds < msPerFrame)
            { /* empty loop body */ }

            watch.Restart();

            Update();

            ServerUpdate?.Invoke(theWorld.Players.Values, theWorld.Powerups.Values, theWorld.Walls.Values);
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

    // From the TIPS section of PS9 instructions 

    /// <summary>
    /// This is a delegate callback passed to the networking class to handle a new client connecting. 
    /// Change the callback for the socketState to a new method that recieves the player's name, then ask for data. 
    /// </summary>
    /// <param name="state">Curent state of a client's socket</param>
    private void NewClientConnected(SocketState state)
    {
        // does the client catch this error? 
        if (state.ErrorOccurred)
            return; // hence why we dont do anything except close the thread here ? 

        // 1)Change the callback for  the SocketState to a new method that recieves the player's name
        state.OnNetworkAction = RecievePlayerName;
        // 2)Ask for Data
        Networking.GetData(state);

        PlayerID++; // each connection to the server means a new snake is added. 
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
        //TODO 
        string playerName = state.GetData(); // not sure if this will work? 

        // 1) Make a new Snake with the given name and unique ID (Recommended using the SocketState'sID). 
        // made a new constructor for this
        // is it safe to convert from long to int?
        Snake snake = new Snake((int)state.ID, playerName);

        // 2) Change the callback to a method that handles command requests from the client. 
        state.OnNetworkAction = HandleSnakeMovementCommands; // After this is called, all Networking.Send calls from the client will be move commands

        // 3) Send the startup info to the client.
        // send player name, world size, all walls


        // 4) Add the client's socket to a list of all clients.
        lock (clients)
        {
            // the clients ID is of type long, this is because its a IP Address 
            clients[state.ID] = state; // this line here makes sure that the client's socket is added to the dictonary
        }

        // 5) Ask for Data
        Networking.GetData(state);


        throw new NotImplementedException();
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
        //TODO
        // 1) Process the command
        // pseudo code 

        // 2) ask for more data
        Networking.GetData(state);

        throw new NotImplementedException();
    }


    /// <summary>
    /// This is the method invoked every iteration through the frame loop. 
    /// Update the world then send it to each client
    /// </summary>
    private void Update()
    {
        IEnumerable<int> playersToRemove = theWorld.Players.Values.Where(x => !x.died).Select(x => x.snake);
        IEnumerable<int> powsToRemove = theWorld.Powerups.Values.Where(x => !x.died).Select(x => x.power);
        while (theWorld.Players.Count < maxPlayers)
        {
            //Snake snake = new Snake(nextPlayerID++,"nameholder", list, direction, 0, false, true, false, true);
            //theWorld.Players.Add(snake.snake, snake);
        }

        while (theWorld.Powerups.Count < maxPowerups)
        {
            Vector2D powerup = new(-3, -2);
            Powerup p = new Powerup(nextPowID++, powerup, false);
            theWorld.Powerups.Add(p.power, p);
        }
    }

    // this can probably be deleted as RecievePlayerName and HandleSnakeMovementCommands handle both of these 
    // in a better separation of concerns practice. 

    ////receives player name and then commands as strings -- up, down, left, right
    ////once a players name is received, sends the player id and size of the world back
    //private void ReceiveMessage(SocketState state)
    //{
    //    if (state.ErrorOccurred)
    //    {
    //        RemoveClient(state.ID);
    //        return;
    //    }


    //    Name = state.GetData(); //receive player name from client

    //    //need to reply with the player ID and the size of the world (hard-coding at 900 for now)
    //    size = 900;
    //    SendMessage(state, $"{PlayerID} + \n{size}"); //once this is called, have server send state of world
    //}

    //commenting out for now -- process movement commands 
    //private void ProcessMessage(SocketState state)
    //{
    //    string totalData = state.GetData();
    //    string[] parts = Regex.Split(totalData, @"(?<=[\n])");

    //    foreach (string p in parts)
    //    {
    //        if (p.Length == 0)
    //            continue;

    //        if (p[p.Length - 1] != '\n')
    //            break;

    //        Console.WriteLine("Received message from client " + state.ID + ": \"" + p.Substring(0, p.Length - 1) + "\"");

    //        state.RemoveData(0, p.Length);

    //        HashSet<long> disconnectedClients = new HashSet<long>();

    //        foreach (SocketState client in clients.Values)
    //        {
    //            if (client != null && client.ID != state.ID)
    //            {
    //                if (!Networking.Send(client.TheSocket!, "Message from client " + state.ID + ": " + p))
    //                    disconnectedClients.Add(client.ID);
    //            }
    //        }

    //        foreach (long id in disconnectedClients)
    //            RemoveClient(id);
    //    }
    //}

    private string JsonWorld()
    {
        //Snakes, powerups, and walls to JSON -- i hardcoded values just testing things out.
        Vector2D direction = new(-1, 0);
        Vector2D head = new(3, 1);
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
                name = Name,
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

    private void SendMessage(SocketState s, string message)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        // Begin sending the message
        Console.WriteLine(message);
        //s.BeginSend(messageBytes, 0, messageBytes.Length, SocketFlags.None, SendCallback, s);

        //have server send state of world in json form
    }

    private void RemoveClient(long id)
    {
        Console.WriteLine("Client " + id + " disconnected");
        clients.Remove(id);

    }

    /// <summary>
    /// Parses XML file and gets settings for the server.
    /// </summary>
    private void ParseSettingsXMLFile()
    {
        // does this need to account for dynamic local paths on a any computer? 
        string relativePath = "C:\\Users\\parke\\source\\repos\\game-bytebuddies_game\\SnakeGame\\settings.xml";
        DataContractSerializer ser;

        FileStream fs = new FileStream(relativePath, FileMode.Open);
        XmlDictionaryReader reader =
        XmlDictionaryReader.CreateTextReader(fs, new XmlDictionaryReaderQuotas());
        while (reader.Read())
        {

            
        }
    }
}
