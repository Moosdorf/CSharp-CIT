using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
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

    public override string? ToString()
    {
        return "NAME:" + Name + "ID" + Id;
    }
}

public class Server
{
    private readonly int _port;
    private static Dictionary<string, Category> categories = new Dictionary<string, Category>();
    const string validPath = "/api/categories";
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
                sendResponse(stream, "4 missing method, missing date, missing path, missing body", null);

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
        // few checks if we have everything we need to proceed
        if (IsRequestOK(stream, request))
        {
            switch (request.Method)
            {
                case "create":
                    int[] path_categories = pathIsOK(stream, request);

                    if (path_categories is not null & path_categories.Length == 1)
                    {
                        Category new_category = createCategory(path_categories[0], request);

                        if (new_category.Name.Equals("") | new_category.Name is null)
                        {
                            sendResponse(stream, "4 Bad Request", null);

                        } else
                        {
                            Console.WriteLine("category ok");
                            Console.WriteLine("go create");
                        }
                    } else
                    {
                        sendResponse(stream, "4 Bad Request", null);
                    }
                    
                    break;

                case "read":
                    HandleRead(stream, request);
                    break;

                case "echo":
                    // send the message back to user and status is 1 Ok, this request has been fulfilled
                    sendResponse(stream, "1 Ok", request.Body);
                    Console.WriteLine("go echo");
                    break;

                case "delete":
                    var delete_category = pathIsOK(stream, request);

                    if (delete_category.Length == 1)
                    {
                        Console.WriteLine("go delete");
                    } else
                    {
                        sendResponse(stream, "4 Bad Request", null); // delete illegal
                    }

                    break;

                case "update":
                    Console.WriteLine("go update");
                    Category category;
                    Request result;
                    try 
                    {
                        var update_categories = pathIsOK(stream, request);
                        if (update_categories.Length == 1)
                        {
                            result = FromJson(request.Body);
                            sendResponse(stream, "3 Updated", null); // update not made
                        }
                        else 
                        {
                            sendResponse(stream, "4 Bad Request", null); // update illegal
                        }



                        break;
                    } 
                    catch 
                    {
                        sendResponse(stream, "4 illegal body", null); // update illegal
                        break;
                    }
  
            }
        }

    }

    private Category createCategory(int index, Request request)
    {
        Category newCategory = JsonSerializer.Deserialize<Category>(request.Body);
        return newCategory;
    }

    private int[] pathIsOK(NetworkStream stream, Request request)
    {
        if (request.Path == validPath) // do all
        {
            return new int[] { 1, 2, 3 }; // FAKE NUMBERS REPLACE WITH REAL IMPLEMENTATION
            // return all numbers
        }
        else // check which category and if it exists
        {
            string lastCharacter = request.Path[^1].ToString();
            try
            {
                int categoryNum = Int32.Parse(lastCharacter);
                // the index might not exist
                return new int[] { categoryNum };
            }
            catch // if not an int, its no good
            {
                sendResponse(stream, "4 Bad Request", null);
            }
        }

        return null;
    }

    private void HandleRead(NetworkStream stream, Request request)
    {
        
        if (pathIsOK(stream, request) is not null)
        {

        }
        else
        {
            sendResponse(stream, "4 Bad Request", null);

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
            Console.WriteLine("no path");
            response += "missing path, ";
        }

        // check if date is ok
        if (request.Date is null)
        {
            Console.WriteLine("no date");
            response += "missing date, ";
            // missing date
        }
        else
        {
            // check if date format is ok
            if (!CheckDate(stream, request)) 
            {
                Console.WriteLine("date wrong");
                sendResponse(stream, "4 illegal date", null);
                return false;
            }

        }

        // check if correct method 
        if (!validMethods.Contains(request.Method)) 
        {
            Console.WriteLine("illegal method");
            response += "illegal method, ";
            // no method that matches
        }
        else // assign method when it's legal
        {
            Console.WriteLine("method ok");
            method = request.Method;
        }

        // body can be null with certain methods (read or delete)
        if (request.Body is null & bodyNullMethods.Contains(method)) 
        {
            return true;

        } else if (request.Body is null & !bodyNullMethods.Contains(method))
        {
            // missing body
            Console.WriteLine("missing body");
            response += "missing body, ";
        } 


        if (response != "")
        {
            response = response.Remove(response.Length - 2);
            response = "4 " + response;
            sendResponse(stream, response, null);
            return false;
        }

        return true;
    }

    private bool CheckDate(NetworkStream stream, Request request)
    {
        try
        {
            // date can be passed to int
            Int32.Parse(request.Date);
            Console.WriteLine("date ok");
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
        Response response = new Response
        {
            Status = status, 
            Body = body
        };
        

        var json = ToJson(response);
        Console.WriteLine(json);
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


