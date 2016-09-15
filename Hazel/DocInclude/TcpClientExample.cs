class TcpClientExample
{
    static void Main(string[] args)
    {
        using (TcpConnection connection = new TcpConnection())
        {
            ManualResetEvent e = new ManualResetEvent(false);

            //Whenever we receive data print the number of bytes and how it was sent
            connection.DataReceived += (object sender, DataReceivedEventArgs a) =>
                Console.WriteLine("Received {0} bytes via {1}!", a.Bytes.Length, a.SendOption);

            //When the end point disconnects from us then release the main thread and exit
            connection.Disconnected += (object sender, DisconnectedEventArgs a) =>
                e.Set();

            //Connect to a server
            connection.Connect(new NetworkEndPoint("127.0.0.1", 4296));

            //Wait until the end point disconnects from us
            e.WaitOne();
        }
    }
}
