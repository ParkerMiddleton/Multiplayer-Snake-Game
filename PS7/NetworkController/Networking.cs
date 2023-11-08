﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Loader;
using System.Text;

namespace NetworkUtil;

public static class Networking
{
    /////////////////////////////////////////////////////////////////////////////////////////
    // Server-Side Code
    ////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Starts a TcpListener on the specified port and starts an event-loop to accept new clients.
    /// The event-loop is started with BeginAcceptSocket and uses AcceptNewClient as the callback.
    /// AcceptNewClient will continue the event-loop.
    /// </summary>
    /// <param name="toCall">The method to call when a new connection is made</param>
    /// <param name="port">The the port to listen on</param>
    /// <return> Active TCPListener for the desired server </return>
    public static TcpListener StartServer(Action<SocketState> toCall, int port)
    {
        TcpListener listener = new(IPAddress.Any, port);


        // 1) creating the listener and starting 
        // 2) begining the event loop for acception new clients to the server. 
        try
        {
            ServerTuple serverTuple = new ServerTuple(toCall, listener);
            listener.Start();
            listener.BeginAcceptSocket(AcceptNewClient, serverTuple);
        }
        catch (Exception ex)
        {
            //TODO: some stuff will happen if a server takes too long to connect to a client. 
        }
        return listener;
    }

    /// <summary>
    /// To be used as the callback for accepting a new client that was initiated by StartServer, and 
    /// continues an event-loop to accept additional clients.
    ///
    /// Uses EndAcceptSocket to finalize the connection and create a new SocketState. The SocketState's
    /// OnNetworkAction should be set to the delegate that was passed to StartServer.
    /// Then invokes the OnNetworkAction delegate with the new SocketState so the user can take action. 
    /// 
    /// If anything goes wrong during the connection process (such as the server being stopped externally), 
    /// the OnNetworkAction delegate should be invoked with a new SocketState with its ErrorOccurred flag set to true 
    /// and an appropriate message placed in its ErrorMessage field. The event-loop should not continue if
    /// an error occurs.
    ///
    /// If an error does not occur, after invoking OnNetworkAction with the new SocketState, an event-loop to accept 
    /// new clients should be continued by calling BeginAcceptSocket again with this method as the callback.
    /// </summary>
    /// <param name="ar">The object asynchronously passed via BeginAcceptSocket. It must contain a tuple with 
    /// 1) a delegate so the user can take action (a SocketState Action), and 2) the TcpListener</param>
    private static void AcceptNewClient(IAsyncResult ar)
    {
        // pull the tuple through 
        ServerTuple networkTuple = (ServerTuple)ar.AsyncState!;
        try
        {
            // attempt to end the AcceptSocket command
            Socket socket = networkTuple.Listener.EndAcceptSocket(ar);
            // create a socket state object that will have its network action changed to the ToCall delegate 
            SocketState socketState = new SocketState(networkTuple.Action, socket);
            socketState.OnNetworkAction = networkTuple.Action;
            // invoke that action
            socketState.OnNetworkAction(socketState);

            //continue the loop
            networkTuple.Listener.BeginAcceptSocket(AcceptNewClient, networkTuple);

        }
        catch (Exception ex)
        {
            // create an error socket with the delegate and an error message
            SocketState errorSocket = new SocketState(networkTuple.Action, ex.Message);
            // no need to set errorSocket's error status to true, this happens already in the constructor
            errorSocket.OnNetworkAction(errorSocket);
            // event loop doesnt continue
            return;
        }
    }

    /// <summary>
    /// Stops the given TcpListener.
    /// </summary>
    public static void StopServer(TcpListener listener)
    {
        listener.Stop();
    }

    /////////////////////////////////////////////////////////////////////////////////////////
    // Client-Side Code
    /////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Begins the asynchronous process of connecting to a server via BeginConnect, 
    /// and using ConnectedCallback as the method to finalize the connection once it's made.
    /// 
    /// If anything goes wrong during the connection process, toCall should be invoked 
    /// with a new SocketState with its ErrorOccurred flag set to true and an appropriate message 
    /// placed in its ErrorMessage field. Depending on when the error occurs, this should happen either
    /// in this method or in ConnectedCallback.
    ///
    /// This connection process should timeout and produce an error (as discussed above) 
    /// if a connection can't be established within 3 seconds of starting BeginConnect.
    /// 
    /// </summary>
    /// <param name="toCall">The action to take once the connection is open or an error occurs</param>
    /// <param name="hostName">The server to connect to</param>
    /// <param name="port">The port on which the server is listening</param>
    public static void ConnectToServer(Action<SocketState> toCall, string hostName, int port)
    {
        // TODO: This method is incomplete, but contains a starting point 
        //       for decoding a host address

        // Establish the remote endpoint for the socket.
        IPHostEntry ipHostInfo;
        IPAddress ipAddress = IPAddress.None;

        // Determine if the server address is a URL or an IP
        try
        {
            ipHostInfo = Dns.GetHostEntry(hostName);
            bool foundIPV4 = false;
            foreach (IPAddress addr in ipHostInfo.AddressList)
                if (addr.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    foundIPV4 = true;
                    ipAddress = addr;
                    break;
                }
            // Didn't find any IPV4 addresses
            if (!foundIPV4)
            {
                SocketState errorSocket = new(toCall, "Couldnt find IP for server ");
                errorSocket.OnNetworkAction(errorSocket);
                return;
            }
        }
        catch (Exception)
        {
            // see if host name is a valid ipaddress
            try
            {
                ipAddress = IPAddress.Parse(hostName);
            }
            catch (Exception ex)
            {
                SocketState errorSocket = new(toCall, ex.Message);
                errorSocket.OnNetworkAction(errorSocket);
                return;
            }
        }

        // Create a TCP/IP socket.
        Socket socket = new(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // This disables Nagle's algorithm (google if curious!)
        // Nagle's algorithm can cause problems for a latency-sensitive 
        // game like ours will be 
        socket.NoDelay = true;

        try
        {
            SocketState asyncSocket = new(toCall, socket);
            socket.BeginConnect(ipAddress, port, ConnectedCallback, asyncSocket);
        }
        catch (Exception ex)
        {
            SocketState errorSocket = new(toCall, ex.Message);
            errorSocket.OnNetworkAction(errorSocket);
            return;
        }

    }

    /// <summary>
    /// To be used as the callback for finalizing a connection process that was initiated by ConnectToServer.
    ///
    /// Uses EndConnect to finalize the connection.
    /// 
    /// As stated in the ConnectToServer documentation, if an error occurs during the connection process,
    /// either this method or ConnectToServer should indicate the error appropriately.
    /// 
    /// If a connection is successfully established, invokes the toCall Action that was provided to ConnectToServer (above)
    /// with a new SocketState representing the new connection.
    /// 
    /// </summary>
    /// <param name="ar">The object asynchronously passed via BeginConnect</param>
    private static void ConnectedCallback(IAsyncResult ar)
    {
        // getting stuff from previous BeginConnect method call
        SocketState socketState = (SocketState)ar.AsyncState!;

        try
        {// ending the connection
            socketState.TheSocket.EndConnect(ar);

        }
        catch (Exception ex)
        {
            SocketState errorSocket = new(socketState.OnNetworkAction, ex.Message);
            errorSocket.OnNetworkAction(errorSocket);
            return;
        }



    }

    /////////////////////////////////////////////////////////////////////////////////////////
    // Server and Client Common Code
    /////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Begins the asynchronous process of receiving data via BeginReceive, using ReceiveCallback 
    /// as the callback to finalize the receive and store data once it has arrived.
    /// The object passed to ReceiveCallback via the AsyncResult should be the SocketState.
    /// 
    /// If anything goes wrong during the receive process, the SocketState's ErrorOccurred flag should 
    /// be set to true, and an appropriate message placed in ErrorMessage, then the SocketState's
    /// OnNetworkAction should be invoked. Depending on when the error occurs, this should happen either
    /// in this method or in ReceiveCallback.
    /// </summary>
    /// <param name="state">The SocketState to begin receiving</param>
    public static void GetData(SocketState state)
    {
        // try / catch ? 
        state.TheSocket.BeginReceive(state.buffer, state.data.Length, state.buffer.Length, SocketFlags.None, ReceiveCallback, state);
    }

    /// <summary>
    /// To be used as the callback for finalizing a receive operation that was initiated by GetData.
    /// 
    /// Uses EndReceive to finalize the receive.
    ///
    /// As stated in the GetData documentation, if an error occurs during the receive process,
    /// either this method or GetData should indicate the error appropriately.
    /// 
    /// If data is successfully received:
    ///  (1) Read the characters as UTF8 and put them in the SocketState's unprocessed data buffer (its string builder).
    ///      This must be done in a thread-safe manner with respect to the SocketState methods that access or modify its 
    ///      string builder.
    ///  (2) Call the saved delegate (OnNetworkAction) allowing the user to deal with this data.
    /// </summary>
    /// <param name="ar"> 
    /// This contains the SocketState that is stored with the callback when the initial BeginReceive is called.
    /// </param>
    private static void ReceiveCallback(IAsyncResult ar)
    {
        SocketState state = (SocketState)ar.AsyncState!;
        // try catch 
        state.TheSocket.EndReceive(ar);

        string data = Encoding.UTF8.GetString(state.buffer);
        state.data.Append(data); // potential error here. 
        state.OnNetworkAction(state);
        state.TheSocket.BeginReceive(state.buffer, state.data.Length, state.buffer.Length, SocketFlags.None, ReceiveCallback, state);

    }

    /// <summary>
    /// Begin the asynchronous process of sending data via BeginSend, using SendCallback to finalize the send process.
    /// 
    /// If the socket is closed, does not attempt to send.
    /// 
    /// If a send fails for any reason, this method ensures that the Socket is closed before returning.
    /// </summary>
    /// <param name="socket">The socket on which to send the data</param>
    /// <param name="data">The string to send</param>
    /// <returns>True if the send process was started, false if an error occurs or the socket is already closed</returns>
    public static bool Send(Socket socket, string data)
    {

        if (!socket.Connected)
        {
            socket.Close();
            return false;
        }
        try
        {
            byte[] message = Encoding.UTF8.GetBytes(data);
            socket.BeginSend(message, 0, message.Length, SocketFlags.None, SendCallback, socket);
            return true;
        }
        catch (Exception ex)
        {
            // This could throw an exception of the socket itself is not connected.
            socket.Close();
            return false;
        }
    }

    /// <summary>
    /// To be used as the callback for finalizing a send operation that was initiated by Send.
    ///
    /// Uses EndSend to finalize the send.
    /// 
    /// This method must not throw, even if an error occurred during the Send operation.
    /// </summary>
    /// <param name="ar">
    /// This is the Socket (not SocketState) that is stored with the callback when
    /// the initial BeginSend is called.
    /// </param>
    private static void SendCallback(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState!;
        socket.EndSend(ar);

    }


    /// <summary>
    /// Begin the asynchronous process of sending data via BeginSend, using SendAndCloseCallback to finalize the send process.
    /// This variant closes the socket in the callback once complete. This is useful for HTTP servers.
    /// 
    /// If the socket is closed, does not attempt to send.
    /// 
    /// If a send fails for any reason, this method ensures that the Socket is closed before returning.
    /// </summary>
    /// <param name="socket">The socket on which to send the data</param>
    /// <param name="data">The string to send</param>
    /// <returns>True if the send process was started, false if an error occurs or the socket is already closed</returns>
    public static bool SendAndClose(Socket socket, string data)
    {
        if (!socket.Connected)
        {
            socket.Close();
            return false;
        }
        try
        {
            byte[] message = Encoding.UTF8.GetBytes(data);
            socket.BeginSend(message, 0, message.Length, SocketFlags.None, SendCallback, socket);
            return true;
        }
        catch (Exception ex)
        {
            // This could throw an exception of the socket itself is not connected.
            socket.Close();
            return false;
        }
    }

    /// <summary>
    /// To be used as the callback for finalizing a send operation that was initiated by SendAndClose.
    ///
    /// Uses EndSend to finalize the send, then closes the socket.
    /// 
    /// This method must not throw, even if an error occurred during the Send operation.
    /// 
    /// This method ensures that the socket is closed before returning.
    /// </summary>
    /// <param name="ar">
    /// This is the Socket (not SocketState) that is stored with the callback when
    /// the initial BeginSend is called.
    /// </param>
    private static void SendAndCloseCallback(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState!;
        socket.EndSend(ar);
        socket.Close();

    }

    internal class ClientTuple
    {
        public Action<SocketState> Action { get; private set; }
        public IPAddress IP { get; private set; }
        public int Port { get; private set; }
        public Socket Socket { get; private set; }
        public ClientTuple(IPAddress ip, int port, Socket socket, Action<SocketState> action)
        {
            IP = ip; Port = port;
            Socket = socket;
            Action = action;
        }
    }

    internal class ServerTuple
    {
        public Action<SocketState> Action { get; private set; }

        public TcpListener Listener { get; private set; }

        public ServerTuple(Action<SocketState> action, TcpListener listener)
        {
            Action = action;
            Listener = listener;
        }
    }
}
