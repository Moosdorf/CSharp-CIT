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
        return "NAME: " + Name + " ID: " + Id;
    }
}

public class Server
{
    private readonly int _port;
    private Dictionary<int, Category> categories;
    const string validPath = "/api/categories";
    public Server(int port){ _port = port; }
    public void Run()
    {
    categories = new Dictionary<int, Category>()
    {
        { 1, new Category { Id = 1, Name = "Beverages"} },
        { 2, new Category { Id = 2, Name = "Condiments"} },
        { 3, new Category { Id = 3, Name = "Confections"} }
    };
    var server = new TcpListener(IPAddress.Loopback, _port);
        server.Start();
        Console.WriteLine($"Server started on {_port}");
        while (true)
        {
            var client = server.AcceptTcpClient();
            Console.WriteLine("Client connected.");

            Task.Run(() => HandleClient(client)); // run a task, will handle the client
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
                SendResponse(stream, "4 missing method, missing date, missing path, missing body", null);
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
                    HandleCreate(stream, request);
                    break;

                case "read":
                    HandleRead(stream, request);
                    break;

                case "echo":
                    // send the message back to user and status is 1 Ok, this request has been fulfilled
                    SendResponse(stream, "1 Ok", request.Body);
                    Console.WriteLine("go echo");
                    break;

                case "delete":
                    HandleDelete(stream, request);
                    break;

                case "update":
                    HandleUpdate(stream, request);
                    return;
            }
        }
    }
    private void HandleUpdate(NetworkStream stream, Request request)
    {
        Request result;

        var update_categories = GetRequestedCategories(stream, request);
        if (update_categories.Count == 1)
        {
            int path = update_categories[0].Id;
            Console.WriteLine("go update");


            try
            {
                categories[path] = CreateCategoryFromRequest(request);
                SendResponse(stream, "3 Updated", null); // update made
            }
            catch
            {
                SendResponse(stream, "4 illegal body", null);
            }




            foreach (var category in categories) { Console.WriteLine(category); }
        } else if (update_categories.Count > 1) 
        {
            SendResponse(stream, "4 Bad Request", null); // update not made
        } 

    }
    private void HandleDelete(NetworkStream stream, Request request)
    {
        var delete_category = GetRequestedCategories(stream, request);

        if (delete_category.Count == 1)
        {
            if (categories.Remove(delete_category[0].Id))
            {
                SendResponse(stream, "1 Ok", null);
            } else
            {
                SendResponse(stream, "5 Not Found", null);
            }
        }
        else if (delete_category.Count > 1)
        {
            SendResponse(stream, "4 Bad Request", null); // cannot delete more than one
        }

    }
    private void HandleCreate(NetworkStream stream, Request request)
    {
        var splitPath = request.Path.Split('/');
        int new_path = default;
        try
        {
            new_path = Int32.Parse(splitPath[^1]); // cannot create new object on already existing
            SendResponse(stream, "4 Bad Request", null);
            return;
        } catch {}

        if (new_path == 0)
        {
            Category new_category = CreateCategoryFromRequest(request);

            if (new_category.Name.Equals("") | new_category.Name is null)
            {
                SendResponse(stream, "4 Bad Request", null);
            }
            else
            {
                new_path = categories.Count + 1;
                new_category.Id = new_path;

                categories.Add(new_path, new_category);
                Console.WriteLine("go create");
                SendResponse(stream, "2 Created", ToJson(new_category));
            }
        }

    }
    private Category CreateCategoryFromRequest(Request request)
    {
        return JsonSerializer.Deserialize<Category>(request.Body); // create a new category from request
    }
    private List<Category> GetRequestedCategories(NetworkStream stream, Request request)
    {
        List<Category> readCategories = new List<Category>();
        List<int> paths = new List<int>();

        if (request.Path == validPath) // do all
        {
            foreach (int key in categories.Keys) 
            { 
                paths.Add(key);
                Console.WriteLine(key);
            }
        }
        else // check which category and if it exists
        {
            string[] splitPath = request.Path.Split('/');
            foreach (string s in splitPath) { Console.WriteLine("SPLIT!: " + s); }
            try
            {
                int categoryNum = Int32.Parse(splitPath[^1]);
                // the index might not exist
                paths.Add(categoryNum);
            }
            catch {
                SendResponse(stream, "4 Bad Request", null);
                return null;
            } // if not an int, its no good

        }

        foreach (int path in paths)
        {
            if (categories.ContainsKey(path))
            {
                readCategories.Add(categories[path]);
            }
            else
            {
                SendResponse(stream, "5 Not Found", null);
                return null;
            }
        }
        return readCategories;
    }
    private void HandleRead(NetworkStream stream, Request request)
    {
        var readPaths = GetRequestedCategories(stream, request);
        if (readPaths is not null)
        {
            Console.WriteLine("READ THESE");



            string responseBody = null;
            // check if categories count is more than one, then return list of categories
            if (readPaths.Count > 1)
            {
                responseBody = JsonSerializer.Serialize(readPaths, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            } else if (readPaths.Count == 1) // if only one, then return that not in a list
            {
                responseBody = JsonSerializer.Serialize(readPaths[0], new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }



            SendResponse(stream, "1 Ok", responseBody);
        }

    }
    private bool IsRequestOK(NetworkStream stream, Request request)
    {
        // “create”, “read”, “update”, “delete”, “echo”
        var validMethods = new string[] { "create", "read", "update", "delete", "echo" };
        var bodyNullMethods = new string[] { "read", "delete" };

        string response = "";
        string method = null;



        // check if date is ok
        if (request.Date is null)
        {
            Console.WriteLine("no date");
            response += "missing resource date, ";
            // missing date
        }
        else
        {
            // check if date format is ok
            if (!CheckDate(stream, request)) 
            {
                Console.WriteLine("date wrong");
                response += "illegal date, ";
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

        // check if path is missing
        if (request.Path is null & method != "echo")
        {
            Console.WriteLine("no path");
            response += "missing resource path, ";
        }

        // body can be null with certain methods (read or delete)
        if (response == "" & request.Body is null & bodyNullMethods.Contains(method)) 
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
            SendResponse(stream, response, null);
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
    private void SendResponse(NetworkStream stream, string status, string body)
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
    public static string ToJson(Category category)
    {
        return JsonSerializer.Serialize(category, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
    public static Request? FromJson(string element)
    {
        return JsonSerializer.Deserialize<Request>(element, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
