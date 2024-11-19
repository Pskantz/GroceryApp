using System;
using Npgsql;

class Program
{
    static void Main(string[] args)
    {
        var connString = "Host=localhost;Username=postgres;Password=asd123;Database=systembolaget";
        using var conn = new NpgsqlConnection(connString);
        conn.Open();

        while (true)
        {
            Console.WriteLine("Välkommen till Systembolaget!");
            Console.WriteLine("1. Logga in");
            Console.WriteLine("2. Registrera");
            Console.WriteLine("3. Avsluta");
            Console.Write("Välj ett alternativ: ");
            var choice = Console.ReadLine();

            if (choice == "1")
                Login(conn);
            else if (choice == "2")
                Register(conn);
            else if (choice == "3")
                break;
            else
                Console.WriteLine("Felaktigt val. Försök igen.");
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
        cmd.Parameters.AddWithValue("u", username ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("p", password ?? (object)DBNull.Value);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            int userId = reader.GetInt32(0);
            Console.WriteLine($"Välkommen {username}!");
            reader.Close();
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
        Console.Write("Ange personnummer (YYYYMMDDXXXX): ");
        var personalNumber = Console.ReadLine();

        if (string.IsNullOrEmpty(personalNumber) || !IsOfAge(personalNumber))
        {
            Console.WriteLine("Du måste vara minst 20 år för att registrera dig.");
            return;
        }

        var cmd = new NpgsqlCommand("INSERT INTO users (username, password, personal_number) VALUES (@u, @p, @pn)", conn);
        cmd.Parameters.AddWithValue("u", username ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("p", password ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("pn", personalNumber);

        try
        {
            cmd.ExecuteNonQuery();
            Console.WriteLine("Användare registrerad!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Kunde inte registrera användare: " + ex.Message);
        }
    }

    static bool IsOfAge(string personalNumber)
    {
        try
        {
            var birthDate = DateTime.ParseExact(personalNumber.Substring(0, 8), "yyyyMMdd", null);
            var age = DateTime.Now.Year - birthDate.Year;
            if (DateTime.Now < birthDate.AddYears(age)) age--;
            return age >= 20;
        }
        catch
        {
            Console.WriteLine("Ogiltigt personnummer.");
            return false;
        }
    }

    static void ShowMenu(NpgsqlConnection conn, int userId)
    {
        while (true)
        {
            Console.WriteLine("\n1. Visa produkter");
            Console.WriteLine("2. Lägg till i kundvagn");
            Console.WriteLine("3. Visa kundvagn");
            Console.WriteLine("4. Slutför köp");
            Console.WriteLine("5. Logga ut");
            Console.Write("Välj ett alternativ: ");
            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    ShowProducts(conn);
                    break;
                case "2":
                    AddToCart(conn, userId);
                    break;
                case "3":
                    ShowCart(conn, userId);
                    break;
                case "4":
                    CompletePurchase(conn, userId);
                    break;
                case "5":
                    return;
                default:
                    Console.WriteLine("Felaktigt val.");
                    break;
            }
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
    }

    // AddToCart, ShowCart, CompletePurchase implementation follows similar structure.
static void AddToCart(NpgsqlConnection conn, int userId)
{
    Console.Write("Ange produkt ID: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input))
    {
        Console.WriteLine("Produkt ID får inte vara tomt.");
        return;
    }
    int productId = int.Parse(input);
    Console.Write("Ange antal: ");
    var quantityInput = Console.ReadLine();
    if (string.IsNullOrEmpty(quantityInput) || !int.TryParse(quantityInput, out int quantity))
    {
        Console.WriteLine("Ogiltigt antal.");
        return;
    }

    // Check product stock
    var stockCmd = new NpgsqlCommand("SELECT stock FROM products WHERE product_id = @p", conn);
    stockCmd.Parameters.AddWithValue("p", productId);
    int stock = Convert.ToInt32(stockCmd.ExecuteScalar());

    if (stock < quantity)
    {
        Console.WriteLine("Det finns inte tillräckligt med lager för att genomföra köpet.");
        return;
    }

    // Add to cart or update quantity
    var cartCmd = new NpgsqlCommand(
        @"INSERT INTO cart (user_id, product_id, quantity) 
          VALUES (@u, @p, @q)
          ON CONFLICT (user_id, product_id) 
          DO UPDATE SET quantity = cart.quantity + @q",
        conn);
    cartCmd.Parameters.AddWithValue("u", userId);
    cartCmd.Parameters.AddWithValue("p", productId);
    cartCmd.Parameters.AddWithValue("q", quantity);
    cartCmd.ExecuteNonQuery();

    // Reduce stock in the product table
    var updateStockCmd = new NpgsqlCommand(
        "UPDATE products SET stock = stock - @q WHERE product_id = @p", conn);
    updateStockCmd.Parameters.AddWithValue("q", quantity);
    updateStockCmd.Parameters.AddWithValue("p", productId);
    updateStockCmd.ExecuteNonQuery();

    Console.WriteLine("Produkten har lagts till i din kundvagn.");
}

 static void ShowCart(NpgsqlConnection conn, int userId)
{
    var cmd = new NpgsqlCommand(
        @"SELECT p.name, c.quantity, p.price, c.quantity * p.price AS total 
          FROM cart c 
          JOIN products p ON c.product_id = p.product_id 
          WHERE c.user_id = @u", conn);
    cmd.Parameters.AddWithValue("u", userId);

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

static void CompletePurchase(NpgsqlConnection conn, int userId)
{
    try
    {
        var cmd = new NpgsqlCommand(
            @"SELECT p.name, c.quantity, p.price 
              FROM cart c 
              JOIN products p ON c.product_id = p.product_id 
              WHERE c.user_id = @u", conn);
        cmd.Parameters.AddWithValue("u", userId);

        using var reader = cmd.ExecuteReader();

        if (!reader.HasRows)
        {
            Console.WriteLine("Din kundvagn är tom. Ingen kan köpas.");
            return; // Avsluta om ingen produkt finns i kundvagnen
        }

        Console.WriteLine("\nKvitto för ditt köp:");
        Console.WriteLine("Produkt | Kvantitet | Pris/st | Total");

        decimal grandTotal = 0;

        // Läs varje rad från kundvagnen och visa information om produkterna
        while (reader.Read())
        {
            string productName = reader.GetString(0); // Produktnamn från 'products' tabell
            int quantity = reader.GetInt32(1);        // Kvantitet från 'cart' tabell
            decimal price = reader.GetDecimal(2);     // Pris per styck från 'products' tabell
            decimal total = quantity * price;         // Totalpris för denna produkt

            Console.WriteLine($"{productName} | {quantity} | {price:C} | {total:C}");
            grandTotal += total; // Lägg till totala priset för denna produkt till grandTotal
        }

        reader.Close(); // Stäng läsaren

        // Töm kundvagnen efter köp
        var clearCartCmd = new NpgsqlCommand("DELETE FROM cart WHERE user_id = @u", conn);
        clearCartCmd.Parameters.AddWithValue("u", userId);
        clearCartCmd.ExecuteNonQuery(); // Utför borttagningen

        // Visa det totala beloppet
        Console.WriteLine($"Totalt belopp att betala: {grandTotal:C}");
        Console.WriteLine("Tack för ditt köp!");
    }
    catch (Exception ex)
    {
        // Fångar alla fel och skriver ut dem
        Console.WriteLine($"Ett fel uppstod vid köpet: {ex.Message}");
    }
}
}
