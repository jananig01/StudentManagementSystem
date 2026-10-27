
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

namespace StudentManagementSystem.Pages;

public class EditModel : PageModel
{
    private readonly IConfiguration _configuration;

    public EditModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    public StudentDetails Student { get; set; } = new();

    public string Message { get; set; } = "";

    private MySqlConnection GetConnection()
    {
        string connectionString =
            _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Database connection string not found.");

        return new MySqlConnection(connectionString);
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        using var connection = GetConnection();
        await connection.OpenAsync();

        string query = @"
            SELECT Id, Name, Age, Course, Email
            FROM Students
            WHERE Id = @Id";

        using var command = new MySqlCommand(query, connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return NotFound();
        }

        Student = new StudentDetails
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Age = reader.GetInt32(reader.GetOrdinal("Age")),
            Course = reader.GetString(reader.GetOrdinal("Course")),
            Email = reader.GetString(reader.GetOrdinal("Email"))
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Student.Name) ||
            string.IsNullOrWhiteSpace(Student.Course) ||
            string.IsNullOrWhiteSpace(Student.Email) ||
            Student.Age < 1 ||
            Student.Age > 120)
        {
            Message = "Please enter valid student details.";
            return Page();
        }

        using var connection = GetConnection();
        await connection.OpenAsync();

        string query = @"
            UPDATE Students
            SET Name = @Name,
                Age = @Age,
                Course = @Course,
                Email = @Email
            WHERE Id = @Id";

        using var command = new MySqlCommand(query, connection);

        command.Parameters.AddWithValue("@Id", Student.Id);
        command.Parameters.AddWithValue("@Name", Student.Name.Trim());
        command.Parameters.AddWithValue("@Age", Student.Age);
        command.Parameters.AddWithValue("@Course", Student.Course.Trim());
        command.Parameters.AddWithValue("@Email", Student.Email.Trim());

        await command.ExecuteNonQueryAsync();

        return RedirectToPage("/Index");
    }

    public class StudentDetails
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string Course { get; set; } = "";
        public string Email { get; set; } = "";
    }
}
