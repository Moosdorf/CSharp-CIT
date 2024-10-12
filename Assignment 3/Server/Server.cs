using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
public class Category
{
    [JsonPropertyName("cid")]
    public int Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class Server
{
    private readonly int _port;
    private static Dictionary<string, Category> categories = new Dictionary<string, Category>();

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
                sendResponse(stream, "4 missing method, missing date, missing path, missing body", "");

            } else
            {
                Request request = FromJson(msg);
                Console.WriteLine(request.ToString());
                if (request == null)
                {
                    // handle that
                }

                // if illegal method, send appropriate response
                HandleRequest(stream, request);

            }

        }
        catch { }
    }

    private void HandleRequest(NetworkStream stream, Request request)
    {
        Console.WriteLine(request);
        // few checks if we have everything we need to proceed
        if (IsRequestOK(stream, request))
        {
            switch (request.Method)
            {
                case "create":
                    Console.WriteLine("go create");
                    break;

                case "read":
                    string validPath = "/api/categories";
                    if (request.Path.Contains(validPath))
                    {
                        if (request.Path == validPath) // do all
                        {

                        } else // check which category and if it exists
                        {
                            string lastCharacter = request.Path[^1].ToString();
                            try
                            {
                                int categoryNum = Int32.Parse(lastCharacter);
                                // the index might not exist
                            }
                            catch // if not an int, its no good
                            {
                                sendResponse(stream, "4 Bad Request", request.Body);
                            }
                        }

                    } else
                    {
                        sendResponse(stream, "4 Bad Request", request.Body);

                    }
                    break;

                case "echo":
                    // send the message back to user and status is 1 Ok, this request has been fulfilled
                    sendResponse(stream, "1 Ok", request.Body);
                    Console.WriteLine("go echo");
                    break;

                case "delete":
                    Console.WriteLine("go delete");
                    break;

                case "update":

                    Category category;
                    Request result;
                    try 
                    {
                        result = FromJson(request.Body);
                        sendResponse(stream, "3 Updated", ""); // update not made
                        break;
                    } 
                    catch 
                    {
                        sendResponse(stream, "4 illegal body", ""); // update not made
                        break;
                    }
  
            }
        }

    }

    private bool IsRequestOK(NetworkStream stream, Request request)
    {
        // “create”, “read”, “update”, “delete”, “echo”
        var validMethods = new string[] { "create", "read", "update", "delete", "echo" };
        var bodyNullMethods = new string[] { "read", "delete" };

        string response = "";
        string method = null;

        // check if path is missing
        if (request.Path is null) 
        {
            response += "missing path, ";
        }

        // check if correct method 
        if (!validMethods.Contains(request.Method)) 
        {
            response += "illegal method, ";
            // no method that matches
        }
        else // assign method when it's legal
        {
            method = request.Method;
        }

        // body can be null with certain methods (read or delete)
        if (request.Body is null & bodyNullMethods.Contains(method)) 
        {
            response += "missing resource, ";

        } else if (bodyNullMethods.Contains(method))
        {
            // missing body
            response += "missing body, ";
        }
        else
        {
            return true;
        }
        if (request.Date is null) // check if date is ok
        {
            response += "missing date, ";
            // missing date
        }
        if (response != "")
        {
            response = response.Remove(response.Length - 2);
            response = "4 " + response;
            sendResponse(stream, response, "");
            return false;
        }



        if (CheckDate(stream, request)) // check if date format is ok
        {
            // do logic
        } else
        {
            sendResponse(stream, "4 illegal date", "");
        }
        return false;
    }

    private bool CheckDate(NetworkStream stream, Request request)
    {
        try
        {
            // date can be passed to int
            Int32.Parse(request.Date);
            return true;
        }
        catch
        {
            // date is not legal
            return false;
        }
    }

    private void sendResponse(NetworkStream stream, string status, string body)
    {
        var response = new Response
        {
            Status = status, Body = body
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


