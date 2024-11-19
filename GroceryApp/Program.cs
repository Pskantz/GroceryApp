using System;
using Npgsql;

class Program
{
    static void Main(string[] args)
    {
        var connString = "Host=localhost;Username=postgres;Database=grocery_store";
        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        Console.WriteLine("Välkommen till matbutiken!");
        Console.WriteLine("1. Logga in");
        Console.WriteLine("2. Registrera");
        Console.WriteLine("Välj ett alternativ: ");
        var choice = Console.ReadLine();

        if (choice == "1")
        {
            Login(conn);
        }
        else if (choice == "2")
        {
            Register(conn);
        }
        else
        {
            Console.WriteLine("Felaktigt val");
        }
    }

    static void Login(NpgsqlConnection conn)
    {
        Console.Write("Ange användarnamn: ");
        var username = Console.ReadLine();
        Console.Write("Ange lösenord: ");
        var password = Console.ReadLine();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Användarnamn och lösenord får inte vara tomma.");
            return;
        }

        var cmd = new NpgsqlCommand("SELECT user_id FROM users WHERE username = @u AND password = @p", conn);
        cmd.Parameters.AddWithValue("u", username ?? string.Empty);
        cmd.Parameters.AddWithValue("p", password ?? string.Empty);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            int userId = reader.GetInt32(0);
            reader.Close();
            Console.WriteLine($"Välkommen, {username}!");
            ShowMenu(conn, userId);
        }
        else
        {
            Console.WriteLine("Felaktigt användarnamn eller lösenord.");
        }
    }

    static void Register(NpgsqlConnection conn)
    {
        Console.Write("Ange användarnamn: ");
        var username = Console.ReadLine();
        Console.Write("Ange lösenord: ");
        var password = Console.ReadLine();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Console.WriteLine("Användarnamn och lösenord får inte vara tomma.");
            return;
        }

        var cmd = new NpgsqlCommand("INSERT INTO users (username, password) VALUES (@u, @p)", conn);
        cmd.Parameters.AddWithValue("u", username ?? string.Empty);
        cmd.Parameters.AddWithValue("p", password ?? string.Empty);

        try
        {
            cmd.ExecuteNonQuery();
            Console.WriteLine("Registrering lyckades!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ett fel inträffade: " + ex.Message);
        }
    }

    static void ShowMenu(NpgsqlConnection conn, int userID)
    {
        Console.WriteLine("\n1. Visa varor");
        Console.WriteLine("2. Lägg till i kundvagn");
        Console.WriteLine("3. Visa kundvagn");
        Console.WriteLine("4. Ta bort från kundvagn");
        Console.WriteLine("5. Slutför köp");
        Console.WriteLine("6. Visa orderhistorik");
        Console.WriteLine("7. Ändra lösenord");
        Console.WriteLine("8. Sök efter produkter");
        Console.WriteLine("9. Logga ut");
        Console.Write("Välj ett alternativ: ");

        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                ShowProducts(conn);
                break;
            case "2":
                AddToCart(conn, userID);
                break;
            case "3":
                ShowCart(conn, userID);
                break;
            case "4":
                RemoveFromCart(conn, userID);
                break;
            case "5":
                CompletePurchase(conn, userID);
                break;
            case "6":
                ViewOrderHistory(conn, userID);
                break;
            case "7":
                UpdatePassword(conn, userID);
                break;
            case "8":
                SearchProducts(conn);
                break;
            case "9":
                Console.WriteLine("Hej då!");
                break;
            default:
                Console.WriteLine("Felaktigt val");
                break;
        }
    }

    static void ShowProducts(NpgsqlConnection conn)
    {
        var cmd = new NpgsqlCommand("SELECT product_id, name, price, stock FROM products", conn);
        using var reader = cmd.ExecuteReader();

        Console.WriteLine("\nProdukter:");
        Console.WriteLine("ID | Namn | Pris | Lager");
        while (reader.Read())
        {
            Console.WriteLine($"{reader.GetInt32(0)} | {reader.GetString(1)} | {reader.GetDecimal(2):C} | {reader.GetInt32(3)}");
        }

        reader.Close();
    }

    static void AddToCart(NpgsqlConnection conn, int userID)
    {
        Console.Write("Ange produkt ID: ");
        var input = Console.ReadLine();
        if (!int.TryParse(input, out int productID))
        {
            Console.WriteLine("Ogiltigt produkt ID");
            return;
        }
        Console.Write("Ange antal: ");
        var inputQuantity = Console.ReadLine();
        if (!int.TryParse(inputQuantity, out int quantity))
        {
            Console.WriteLine("Ogiltigt antal");
            return;
        }

        var cmd = new NpgsqlCommand("SELECT stock FROM products WHERE product_id = @p", conn);
        cmd.Parameters.AddWithValue("p", productID);

        int stock = 0;
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            stock = reader.GetInt32(0);
        }
        else
        {
            Console.WriteLine("Produkt hittades inte");
            return;
        }

        if (quantity > stock)
        {
            Console.WriteLine("För få i lager");
            return;
        }

        cmd = new NpgsqlCommand(
            "INSERT INTO cart (user_id, product_id, quantity) VALUES (@u, @p, @q) ON CONFLICT (user_id, product_id) DO UPDATE SET quantity = cart.quantity + @q", conn);
        cmd.Parameters.AddWithValue("u", userID);
        cmd.Parameters.AddWithValue("p", productID);
        cmd.Parameters.AddWithValue("q", quantity);
        cmd.ExecuteNonQuery();

        cmd = new NpgsqlCommand("UPDATE products SET stock = stock - @q WHERE product_id = @p", conn);
        cmd.Parameters.AddWithValue("q", quantity);
        cmd.Parameters.AddWithValue("p", productID);
        cmd.ExecuteNonQuery();

        Console.WriteLine("Produkt tillagd i kundvagn");
    }

    static void ShowCart(NpgsqlConnection conn, int userID)
    {
        var cmd = new NpgsqlCommand("SELECT p.name, c.quantity, p.price, c.quantity * p.price AS total FROM cart c JOIN products p ON c.product_id = p.product_id WHERE c.user_id = @u", conn);
        cmd.Parameters.AddWithValue("u", userID);

        using var reader = cmd.ExecuteReader();

        Console.WriteLine("\nDin kundvagn:");
        Console.WriteLine("Namn | Kvantitet | Pris/st | Total");
        decimal grandTotal = 0;
        while (reader.Read())
        {
            Console.WriteLine($"{reader.GetString(0)} | {reader.GetInt32(1)} | {reader.GetDecimal(2):C} | {reader.GetDecimal(3):C}");
            grandTotal += reader.GetDecimal(3);
        }

        reader.Close();

        Console.WriteLine($"Totalt: {grandTotal:C}");
    }

    static void RemoveFromCart(NpgsqlConnection conn, int userID)
    {
        Console.Write("Ange produkt ID: ");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input) || !int.TryParse(input, out int productID))
        {
            Console.WriteLine("Ogiltigt produkt ID");
            return;
        }
        Console.Write("Ange antal: ");
        var inputQuantity = Console.ReadLine();
        if (!int.TryParse(inputQuantity, out int quantity))
        {
            Console.WriteLine("Ogiltigt antal");
            return;
        }

        var cmd = new NpgsqlCommand("SELECT quantity FROM cart WHERE user_id = @u AND product_id = @p", conn);
        cmd.Parameters.AddWithValue("u", userID);
        cmd.Parameters.AddWithValue("p", productID);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            int cartQuantity = reader.GetInt32(0);
            if (quantity > cartQuantity)
            {
                Console.WriteLine("För få i kundvagn");
                return;
            }

            cmd = new NpgsqlCommand("DELETE FROM cart WHERE user_id = @u AND product_id = @p", conn);
            cmd.Parameters.AddWithValue("u", userID);
            cmd.Parameters.AddWithValue("p", productID);
            cmd.ExecuteNonQuery();

            cmd = new NpgsqlCommand("UPDATE products SET stock = stock + @q WHERE product_id = @p", conn);
            cmd.Parameters.AddWithValue("q", quantity);
            cmd.Parameters.AddWithValue("p", productID);
            cmd.ExecuteNonQuery();

            Console.WriteLine("Produkt borttagen från kundvagn");
        }
        else
        {
            Console.WriteLine("Produkt hittades inte i kundvagn");
        }
    }

    static void CompletePurchase(NpgsqlConnection conn, int userID)
    {
        var cmd = new NpgsqlCommand("SELECT p.product_id, c.quantity FROM cart c JOIN products p ON c.product_id = p.product_id WHERE c.user_id = @u", conn);
        cmd.Parameters.AddWithValue("u", userID);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int productID = reader.GetInt32(0);
            int quantity = reader.GetInt32(1);

            var cmd2 = new NpgsqlCommand("INSERT INTO orders (user_id, product_id, quantity, order_date) VALUES (@u, @p, @q, CURRENT_TIMESTAMP)", conn);
            cmd2.Parameters.AddWithValue("u", userID);
            cmd2.Parameters.AddWithValue("p", productID);
            cmd2.Parameters.AddWithValue("q", quantity);
            cmd2.ExecuteNonQuery();
        }

        reader.Close();

        var cmd3 = new NpgsqlCommand("DELETE FROM cart WHERE user_id = @u", conn);
        cmd3.Parameters.AddWithValue("u", userID);
        cmd3.ExecuteNonQuery();

        Console.WriteLine("Köp slutfört!");
    }

    static void ViewOrderHistory(NpgsqlConnection conn, int userID)
    {
        var cmd = new NpgsqlCommand(
            "SELECT o.order_id, p.name, o.quantity, p.price, o.quantity * p.price AS total, o.order_date " +
            "FROM orders o " +
            "JOIN products p ON o.product_id = p.product_id " +
            "WHERE o.user_id = @u " +
            "ORDER BY o.order_date DESC", conn);

        cmd.Parameters.AddWithValue("u", userID);

        using var reader = cmd.ExecuteReader();

        Console.WriteLine("\nOrderhistorik:");
        Console.WriteLine("Order ID | Produkt | Kvantitet | Pris/st | Total | Datum");
        while (reader.Read())
        {
            Console.WriteLine($"{reader.GetInt32(0)} | {reader.GetString(1)} | {reader.GetInt32(2)} | {reader.GetDecimal(3):C} | {reader.GetDecimal(4):C} | {reader.GetDateTime(5)}");
        }

        reader.Close();
    }

    static void UpdatePassword(NpgsqlConnection conn, int userID)
    {
        Console.Write("Ange nytt lösenord: ");
        var newPassword = Console.ReadLine();

        if (string.IsNullOrEmpty(newPassword))
        {
            Console.WriteLine("Lösenord får inte vara tomt.");
            return;
        }

        var cmd = new NpgsqlCommand(
            "UPDATE users SET password = @p WHERE user_id = @u", conn);

        cmd.Parameters.AddWithValue("p", newPassword);
        cmd.Parameters.AddWithValue("u", userID);

        try
        {
            cmd.ExecuteNonQuery();
            Console.WriteLine("Lösenord uppdaterat!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Kunde inte uppdatera lösenord: " + ex.Message);
        }
    }

    static void SearchProducts(NpgsqlConnection conn)
    {
        Console.Write("Ange sökterm: ");
        var searchTerm = Console.ReadLine();

        var cmd = new NpgsqlCommand(
            "SELECT product_id, name, price, stock FROM products WHERE name ILIKE @search", conn);

        cmd.Parameters.AddWithValue("search", $"%{searchTerm}%");

        using var reader = cmd.ExecuteReader();

        Console.WriteLine("\nSökresultat:");
        Console.WriteLine("ID | Namn | Pris | Lager");
        while (reader.Read())
        {
            Console.WriteLine($"{reader.GetInt32(0)} | {reader.GetString(1)} | {reader.GetDecimal(2):C} | {reader.GetInt32(3)}");
        }

        reader.Close();
    }
}
