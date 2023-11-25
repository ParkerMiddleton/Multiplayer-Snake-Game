using NetworkUtil;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SnakeGame;


/// <summary>
/// Controller class for the snake project. 
/// 
/// This bridges the gap between the view and the model. Information is sent back to the view via delagates to avoid 
/// circular dependency issues. This class manages JSON data and updates theWorld field with updated world state information. 
/// </summary>
public class GameController
{
    private World theWorld = new World(0);
    private int PlayerID = -1;
    private string? playerName;
    SocketState? theServer = null;


    /// <summary>
    /// Delegate and event combo for notifying the View of an error
    /// Note: Error message will be sent back to the View in this case
    /// </summary>
    /// <param name="error">Error message to be sent to the viee</param>
    public delegate void ErrorHandler(string error);
    public event ErrorHandler? Error;

    /// <summary>
    /// A delegate and event to fire when the controller
    /// has received and processed new info from the server
    /// </summary>
    public delegate void GameUpdateHandler();
    public event GameUpdateHandler? UpdateArrived;

    /// <summary>
    /// Starts the process for connecting to the server. 
    /// </summary>
    /// <param name="ServerText">The DNS name of a server, or a server's IP</param>
    /// <param name="nameText">The name of the player</param>
    public void Connect(string ServerText, string nameText)
    {
        Networking.ConnectToServer(OnConnect, ServerText, 11000);
        playerName = nameText;
    }

    /// <summary>
    /// Method to be invoked by the networking library when a connection is made
    /// </summary>
    /// <param name="state">A sockets current state</param>
    private void OnConnect(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            Error?.Invoke("Error connecting to server");
            return;
        }
        theServer = state;
        //Sends player name to start the handshake
        MessageEntered(playerName!);

        state.OnNetworkAction = ReceiveMessage;
        Networking.GetData(state);
    }

    /// <summary>
    /// Takes in data from a socket state
    /// </summary>
    /// <param name="state">A sockets current state</param>
    private void ReceiveMessage(SocketState state)
    {
        if (state.ErrorOccurred)
        {
            // inform the view
            Error?.Invoke("Lost connection to server");
            return;
        }
        ProcessMessages(state);
        Networking.GetData(state);
    }

    /// <summary>
    /// Process any buffered messages separated by '\n'
    /// Then inform the view
    /// </summary>
    /// <param name="state">A socket's current state</param>
    private void ProcessMessages(SocketState state)
    {
        string totalData = state.GetData();
        string[] parts = Regex.Split(totalData, @"(?<=[\n])");
        List<string> newMessages = new List<string>();

        foreach (string p in parts)
        {
            if (p.Length == 0)
                continue;

            if (p[p.Length - 1] != '\n')
                break;

            //Assigns the first two numbers to their appropriate spots
            if (int.TryParse(p, out int numberNotJSON))
            {
                if (PlayerID == -1)
                {
                    PlayerID = numberNotJSON;
                }
                else
                {
                    theWorld = new World(numberNotJSON);
                }
            }
            else
            {
                JsonUpdater(p);
            }

            // build a list of messages to send to the view
            newMessages.Add(p);

            // Then remove it from the SocketState's growable buffer
            state.RemoveData(0, p.Length);
        }
    }

    /// <summary>
    /// Closes the connection with the server
    /// </summary>
    public void Close()
    {
        theServer?.TheSocket.Close();
    }

    /// <summary>
    /// Send a message to the server
    /// </summary>
    /// <param name="message">Message</param>
    public void MessageEntered(string message)
    {
        if (theServer is not null)
            Networking.Send(theServer.TheSocket, message + "\n");
    }

    /// <summary>
    /// Extracts JSON and turns data into objects that are then added to the world.
    /// </summary>
    /// <param name="data">Json String terminated by a "\n"</param>
    private void JsonUpdater(string data)
    {
        try
        {
            JsonDocument doc = JsonDocument.Parse(data);
            lock (theWorld)
            {

                if (doc.RootElement.TryGetProperty("snake", out JsonElement snakeCheck))
                {
                    Snake? snake = doc.Deserialize<Snake>();
                    if (!theWorld.Players.ContainsKey(snake!.snake))
                    {
                        theWorld!.Players.Add(snake!.snake, snake);
                    }
                    else
                    {
                        theWorld.Players[snake!.snake] = snake;
                    }
                }
                else if (doc.RootElement.TryGetProperty("wall", out JsonElement wallCheck))
                {

                    Wall? wall = doc.Deserialize<Wall>();
                    theWorld!.Walls.Add(wall!.wall, wall);
                }
                else if (doc.RootElement.TryGetProperty("power", out JsonElement powerupCheck))
                {

                    Powerup? powerup = doc.Deserialize<Powerup>();
                    if (!theWorld.Powerups.ContainsKey(powerup!.power))
                    {
                        theWorld!.Powerups.Add(powerup!.power, powerup);
                    }
                    else
                    {
                        theWorld!.Powerups[powerup!.power] = powerup;
                    }

                }
                //if the client has recieved the information about world size and playerID
                if (theWorld.Players.Count > 0 && theWorld.size > 0)
                {
                    UpdateArrived?.Invoke();
                }

            }
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// Getter for the World object. 
    /// </summary>
    /// <returns>World State</returns>
    public World GetWorld()
    {
        return theWorld!;
    }

    /// <summary>
    /// Getter for current Player ID as sent by the server. 
    /// </summary>
    /// <returns></returns>
    public int GetPlayerID()
    {
        return PlayerID;
    }

    /// <summary>
    /// Sends a direction command to the server. 
    /// </summary>
    /// <param name="direction"></param>
    public void SetDirection(string direction)
    {
        MessageEntered("{\"moving\":\"" + direction + "\"}");
    }


}