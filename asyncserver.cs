using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
//using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AsyncServer {

    public class Client {
        /*
         * class Client
         * 
         * if I understand this correctly, we will likely want to 
         * wrap this class object in a real client object for the 
         * mud server.  Then we can use that object to access this
         * object for sending a recieving to the player.
         * 
         * streambuff is the main incoming buffer from the stream
         * limited to 1024 by BufferSize.  The async keeps reading
         * this and applying it to the StringBuilder "buffer"
         * 
         * buffer is the main storaged of incoming data until an
         * EOF (or in our case ^M) is found and the data is ready
         * to be send to the game for processing.
         */
        public Socket socket = null;
        public const int BufferSize = 1024;
        public byte[] streambuffer = new byte[BufferSize];
        public StringBuilder buffer = new StringBuilder();
        public string client_id;
    }

    public class AsyncSocketListener {
        /*
         * class AsyncSocketListner
         * 
         * I've made this class the main server object.
         * It holds most of the information and also 
         * includes a list connected Client objects
         * called players.
         * 
         * It currently works, but for some reason
         * after a while sitting idle, it stops functioing
         * and I end up having to kill it.   Not quite
         * clear where it's locking up, but it might 
         * help to add a logger and very verbose debug 
         * logging to find the issue.
         * 
         */
        public static AutoResetEvent allDone = new AutoResetEvent(false);
        public AsyncSocketListener() { }
        public Dictionary<string, Client> players = new Dictionary<string, Client>();
        public int client_count;
        


        public void StartListening(IPAddress ip, int port, int max_conns) { 
            byte[] bytes = new byte[1024000];
            IPAddress _ip = ip;
            int _port = port;
            int _max_connections = max_conns;
            IPEndPoint _ep = new IPEndPoint(_ip, _port);

            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try {
                listener.Bind(_ep);
                listener.Listen(_max_connections);
                Console.WriteLine("Starting listener...");
                while (true) {
                    
                    allDone.Set();
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    allDone.WaitOne();
                }
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }

        public void AcceptCallback(IAsyncResult AsyncResult) {
            allDone.Set();
            Socket listener = (Socket)AsyncResult.AsyncState;
            Socket handler = listener.EndAccept(AsyncResult);
            Client client = new Client();
            client_count++;
            client.client_id = handler.RemoteEndPoint.ToString();
            client.socket = handler;
            Console.WriteLine(String.Format("Client number {0} to connected as {1}", client_count, client.client_id));
            // the Send below crashes, probably due to it's not a synchronized send?  Not sure.
            //Send(client, "Welcome to SonzoAsyncServer 0.1");
            players.Add(client.client_id, client);
            
            
            handler.BeginReceive(client.streambuffer, 0, Client.BufferSize, 0, new AsyncCallback(ReadCallback), client);
        }

        public void ReadCallback(IAsyncResult AsyncResult) {
            String content = String.Empty;
            Client client = (Client)AsyncResult.AsyncState;
            Socket handler = client.socket;
            int inputbuffer = handler.EndReceive(AsyncResult);

            if (inputbuffer > 0) {
                client.buffer.Append(Encoding.ASCII.GetString(client.streambuffer, 0, inputbuffer));
                content = client.buffer.ToString();
                if (content.IndexOf('\n') > -1) {
                    Console.Write("{0} said: {1}", client.client_id, content);
                    String outstring = String.Format("User from {0} said, {1}", client.client_id, content);
                    SendToAllPlayers(StripNonAscii(outstring));
                } else {
                    handler.BeginReceive(client.streambuffer, 0, Client.BufferSize, 0, new AsyncCallback(ReadCallback), client);
                }
            }
        }

        public void Send(Client player, String data) {
            byte[] databytes = Encoding.ASCII.GetBytes(data + '\n');
            Socket handler = player.socket;
            handler.BeginSend(databytes, 0, databytes.Length, 0, new AsyncCallback(SendCallback), player);
            player.buffer.Clear();
        }

        public void SendToAllPlayers(String data) {
            foreach (KeyValuePair<string, Client> player in players) {
                Send(player.Value, data);
            }
        }
        // ISSUES
        private void SendCallback(IAsyncResult AsyncResult) {
            try {
                Client client = (Client)AsyncResult.AsyncState;
                Socket handler = client.socket;
                int bytessent = handler.EndSend(AsyncResult);
                handler.BeginReceive(client.streambuffer, 0, Client.BufferSize, 0, new AsyncCallback(ReadCallback), client);
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
        }


        public static int Main(String[] args) {
            AsyncSocketListener server = new AsyncSocketListener();
            if(server.players == null) {
                Console.WriteLine("Failed to create server.");
            }
            server.StartListening(IPAddress.Parse("0.0.0.0"), 23, 10);
            return 0;
        }
        public static string StripNonAscii(string value) {
            string pattern = "[^ -~]+";
            Regex reg_exp = new Regex(pattern);
            return reg_exp.Replace(value, "");
        }
    }

}