using NetworkUtil;
using System.Text.Json.Serialization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SnakeGame
{
    /// <summary>
    /// The GameController is responsible for parsing information received from the NetworkController (USE HERE), 
    /// and updating the model. After updating the model, it should then inform (or invoke) the view that the world 
    /// has changed, so that it can redraw. It should happen via event. 
    /// 
    /// The GameController should also define methods that the View can invoke whenever certain user inputs occur,
    /// then decide which messages to send to the server, requesting that the snake turns. 
    /// </summary>
    public class GameController
    {
        [JsonInclude]
        string? moving;

        /// <summary>
        /// a delegate and event combo for talking back to the view and sending data to the server and model
        /// Note: parameters for the delegates here only are used to send data back to the view
        /// </summary>
        public delegate void ConnectedHandler(); 
        public event ConnectedHandler? Connected;

        /// <summary>
        /// Delegate and event combo for notifying the View of an error
        /// Note: Error message will be sent back to the View in this case
        /// </summary>
        /// <param name="error"></param>
        public delegate void ErrorHandler(string error);
        public event ErrorHandler? Error;




        /// <summary>
        /// represents the state of the server connection
        /// </summary>
        SocketState? theServer = null;


        /// <summary>
        /// Starts the process for connecting to the server. 
        /// </summary>
        /// <param name="ServerText"></param>
        public void Connect(string ServerText)
        {
            Networking.ConnectToServer(OnConnect, ServerText, 11000);
        }

        /// <summary>
        /// Method to be invoked by the networking library when a connection is made
        /// </summary>
        /// <param name="state"></param>
        private void OnConnect(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                // inform the view
                Error?.Invoke("Error connecting to server");
                return;
            }

            // inform the view
            Connected?.Invoke();

            theServer = state;

            // Start an event loop to receive messages from the server
            // in our case this should be the "handshake" data.
            //state.OnNetworkAction = ReceiveMessage;
            //Networking.GetData(state);
        }








    }
}