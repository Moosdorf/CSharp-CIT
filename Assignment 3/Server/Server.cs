using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


public class Server
{
    private readonly int _port;

    public Server(int port)
    {
        _port = port;
    }
    public void Run()
    {
        var server = new TcpListener(IPAddress.Loopback, _port);
        server.Start();

        Console.WriteLine($"Server started on {_port}");

        while (true)
        {
            var clinet = server.AcceptTcpClient();
            Console.WriteLine("Client connected.");

            Task.Run(() => HandleClient(clinet)); // run a task, will handle the client
            // instead of running the task directly, it starts a new thread to run clients


        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            var stream = client.GetStream();
            string msg = ReadFromStream(stream);
            Console.WriteLine($"Message from client: {msg}");

            if (msg == "{}")
            {

                sendResponse(stream, "missing method");

            } else
            {
                Request request = FromJson(msg);
                Console.WriteLine(request.Date);
                if (request == null)
                {

                }

                string[] validMethods = ["create", "read", "update", "delete", "echo"]; // we can only use these methods

                // if illegal method, send appropriate response
                if (!validMethods.Contains(request.Method)) { sendResponse(stream, "illegal method"); } 
                else
                {
                    // few checks if we have everything we need to proceed
                    if (request.Body is null) { sendResponse(stream, "missing resource"); }
                    if (request.Date == "{}") { sendResponse(stream, "missing date"); }

                }
            }

        }
        catch { }
    }

    private void sendResponse(NetworkStream stream, string status)
    {
        var response = new Response
        {
            Status = status
        };
        Console.WriteLine(response.Status);
        var json = ToJson(response);
        WriteToStream(stream, json);
    }

    private string ReadFromStream(NetworkStream stream)
    {
        var buffer = new byte[1024];
        var readCount = stream.Read(buffer);
        return Encoding.UTF8.GetString(buffer, 0, readCount); // only read the bytes that are used. 
    }

    private void WriteToStream(NetworkStream stream, string msg)
    {
        var buffer = Encoding.UTF8.GetBytes(msg);
        stream.Write(buffer);

    }


    public static string ToJson(Response response)
    {
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public static Request? FromJson(string element)
    {
        return JsonSerializer.Deserialize<Request>(element, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}


