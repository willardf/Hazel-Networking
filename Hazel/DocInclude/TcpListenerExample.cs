class TcpListenerExample
{
    static void Main(string[] args)
    {
        //Setup listener
        using (TcpConnectionListener listener = new TcpConnectionListener(new NetworkEndPoint(IPAddress.Any, 4296)))
        {
            //Start listening for new connection events
            listener.NewConnection += delegate(object sender, NewConnectionEventArgs a)
            {
                //Send the client some data
                a.Connection.SendBytes(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, SendOption.Reliable);

                //Disconnect from the client
                a.Connection.Close();
            };

            listener.Start();

            Console.ReadKey();
        }
    }
}
