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

    public override string? ToString() // to display the category in console
    {
        return "NAME: " + Name + " ID: " + Id;
    }
}

public class Server
{
    // define port, the database and the valid path 
    private readonly int _port;
    private Dictionary<int, Category> categories;
    const string validPath = "/api/categories";

    // constructor
    public Server(int port){ _port = port; }
    public void Run()
    {
        // fake database initialization, dictionary to keep the categories
        categories = new Dictionary<int, Category>() 
        {
            { 1, new Category { Id = 1, Name = "Beverages"} },
            { 2, new Category { Id = 2, Name = "Condiments"} },
            { 3, new Category { Id = 3, Name = "Confections"} }
        }; 

        // connect and start server
        var server = new TcpListener(IPAddress.Loopback, _port);
        server.Start();
        Console.WriteLine($"Server started on {_port}");

        // count for clients
        int clientCount = 0;

        while (true)
        {
            // wait for client
            var client = server.AcceptTcpClient(); 
            clientCount++; // when client has connected
            Console.WriteLine("Client connected, count: " + clientCount);

            // start a new thread to handle this client
            Task.Run(() => HandleClient(client)); 
            
        }
    }

    // handles the client request
    private void HandleClient(TcpClient client)
    {
        try
        {
            Console.WriteLine("Handle client:");

            // get and read message from client
            var stream = client.GetStream();
            string msg = ReadFromStream(stream);
            Console.WriteLine($"Message from client: {msg}");

            if (msg == "{}") // if empty msg, send respone
            {
                SendResponse(stream, "4 missing method, missing date, missing path, missing body", null);
            } else
            {
                // try to obtain request from json
                Request request = FromJson(msg);

                if (request == null) // if request is null something went wrong
                {
                    SendResponse(stream, "6 Error", null);
                }

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
            // if request is ok, then we can proceed with the methods
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
                    Console.WriteLine("go echo");
                    SendResponse(stream, "1 Ok", request.Body);
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

    // checks if request is ok, if yes then we proceed to the methods (can still fail later if bad request or not found)
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
            Console.WriteLine("body null but ok");
            return true;

        }
        else if (request.Body is null & !bodyNullMethods.Contains(method))
        {

            // missing body
            Console.WriteLine("missing body");
            response += "missing body, ";
        }
        else
        {
            Console.WriteLine("Body ok");
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

    // all methods except echo
    private void HandleCreate(NetworkStream stream, Request request)
    {
        // checking if the path has a specific index (it should not have one)
        var splitPath = request.Path.Split('/');
        int new_path = default;
        try
        {
            new_path = Int32.Parse(splitPath[^1]); // cannot create new object on already existing
            Console.WriteLine("index should not be specified"); // if index has been found then it's a bad request
            SendResponse(stream, "4 Bad Request", null);
            return;
        }
        catch { }

        if (new_path == 0) // if new_path is still default
        {
            // create new category based on the request
            Category new_category = CreateCategoryFromRequest(request);

            // if the name is null or empty then it's a bad request
            if (new_category.Name.Equals("") | new_category.Name is null)
            {
                Console.WriteLine("no name of category");
                SendResponse(stream, "4 Bad Request", null);

            }
            else
            {
                // find new cid for the new category
                new_path = categories.Count + 1;

                // set the id, it was not initialized with one
                new_category.Id = new_path;

                // add the category to the database (dictionary)
                categories.Add(new_path, new_category);
                Console.WriteLine("go create category");
                SendResponse(stream, "2 Created", ToJson(new_category));
            }
        }

    }
    private void HandleRead(NetworkStream stream, Request request)
    {
        // get all categories the client want to read
        var readPaths = GetRequestedCategories(stream, request);
        if (readPaths is not null)
        {
            string responseBody = null;
            // check if categories count is more than one, then return list of categories
            if (readPaths.Count > 1)
            {
                responseBody = JsonSerializer.Serialize(readPaths, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            }
            else if (readPaths.Count == 1) // if only one, then return that not in a list
            {
                responseBody = JsonSerializer.Serialize(readPaths[0], new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            }
            else
            { // if no categories
                SendResponse(stream, "5 Not Found", null);
                return;
            }


            // send response to client with the categories
            SendResponse(stream, "1 Ok", responseBody);
        }

    }
    private void HandleDelete(NetworkStream stream, Request request)
    {
        // get all categories to delete (should only be one)
        var delete_category = GetRequestedCategories(stream, request);

        // if there's only one
        if (delete_category.Count == 1)
        {
            if (categories.Remove(delete_category[0].Id)) // if we can remove it from the database (dictionary)
            {
                Console.WriteLine("deleted ok");
                SendResponse(stream, "1 Ok", null); // has been deleted
            } else
            {
                SendResponse(stream, "5 Not Found", null); // if the path is not ok, then we don't delete it
            }
        }
        else 
        {
            SendResponse(stream, "4 Bad Request", null); // cannot delete if more than one category or 0.
        }

    }
    private void HandleUpdate(NetworkStream stream, Request request)
    {
        // get all categories based on request (should only return one)
        var update_categories = GetRequestedCategories(stream, request);
        if (update_categories.Count == 1) // we can only update one item
        {
            int path = update_categories[0].Id; // the index for the category is the same as cid
            try
            {
                categories[path] = CreateCategoryFromRequest(request); // try to update the category
                Console.WriteLine("go update");
                SendResponse(stream, "3 Updated", null); // update made
                foreach (var category in categories) { Console.WriteLine(category); } // display in the console the whole updated database
            }
            catch // will go into catch is the update cannot be made (illegal body or illegal path)
            {
                SendResponse(stream, "4 illegal body", null); // update not made
            }

        }
        else // if we get too many categories or none
        {
            SendResponse(stream, "4 Bad Request", null); // update not made
        }
    }

    // checks if request has been made within 24 hours (subject to change)
    private bool CheckDate(NetworkStream stream, Request request)
    {
        var twentyFourHours = 24 * 60 * 60;
        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        try
        {
            // if date can be passed to int, then it might be in unix
            var clientRequestDate = Int32.Parse(request.Date); // client request time

            // check if the request has happened in the last 24 hrs (subject to change)
            if (clientRequestDate > (now - twentyFourHours) & clientRequestDate <= now)

            {
                Console.WriteLine("date ok");
                return true;
            }
            else // too old request, or invalid date (date not legal)
            {
                return false;
            }

        }
        catch
        {
            // date is not legal
            return false;
        }
    }

    // get or create category
    private Category CreateCategoryFromRequest(Request request)
    {
        return JsonSerializer.Deserialize<Category>(request.Body); // create a new category from request
    }
    private List<Category> GetRequestedCategories(NetworkStream stream, Request request)
    {
        // get all categories based on request

        // initialize list to keep the paths and one for the categories we are fetching
        List<int> paths = new List<int>();
        List<Category> readCategories = new List<Category>();

        // if the path is exactly our validPath, then we must return all the categories
        if (request.Path == validPath) // do all
        {
            foreach (int key in categories.Keys) // add all keys
            { 
                paths.Add(key);
            }
        }
        else if (request.Path.Contains(validPath)) // check which category and if it exists (must be  l)
        {
            string[] splitPath = request.Path.Split('/'); // split the path
            Console.WriteLine("Try to parse: " + splitPath[^1]); // we only care about the last bit, it's the index
            try
            {
                // try to parse the number (if it is a number)
                int categoryNum = Int32.Parse(splitPath[^1]);
                // the index might not exist
                paths.Add(categoryNum);
            }
            catch // if not an int, it's no good
            {
                
                SendResponse(stream, "4 Bad Request", null);
                return null;
            } 

        } else // if the path is incorrect it's a bad request. Must contain the validPath ("api/categories").
        {
            SendResponse(stream, "4 Bad Request", null);
            return null;
        }

        foreach (int path in paths) // insert all the categories based on the paths found
        {
            if (categories.ContainsKey(path))
            {
                readCategories.Add(categories[path]);
            }
            else
            { // if key does not match any in the database
                SendResponse(stream, "5 Not Found", null);
                return null;
            }
        }
        return readCategories;
    }
    
    // send to or read from client
    private void SendResponse(NetworkStream stream, string status, string body)
    {
        // create the response based on arguments passed
        Response response = new Response
        {
            Status = status, 
            Body = body
        };

        // convert the response to json
        var json = ToJson(response);
        Console.WriteLine("Response: " + json);

        // and send the response to the client
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

    // conversion
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
