
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace StudentManagementSystem.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public IndexModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<Student> Students { get; set; } = new();

        // Search and filter properties
        [BindProperty(SupportsGet = true)]
        public string SearchName { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string FilterCourse { get; set; } = string.Empty;

        public List<string> Courses { get; set; } = new();

        // Dashboard statistics
        public int TotalStudents { get; set; }

        public int TotalCourses { get; set; }

        [BindProperty]
        public Student NewStudent { get; set; } = new();

        public string Message { get; set; } = string.Empty;

        private MySqlConnection GetConnection()
        {
            string connectionString =
                _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection is missing from configuration.");

            return new MySqlConnection(connectionString);
        }

        public async Task OnGetAsync()
        {
            await LoadStudentsAsync();
        }

        // Add Student
        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(NewStudent.Name) ||
                string.IsNullOrWhiteSpace(NewStudent.Course) ||
                string.IsNullOrWhiteSpace(NewStudent.Email))
            {
                Message = "Please fill in all student details.";
                await LoadStudentsAsync();
                return Page();
            }

            if (NewStudent.Age < 1 || NewStudent.Age > 120)
            {
                Message = "Age must be between 1 and 120.";
                await LoadStudentsAsync();
                return Page();
            }

            try
            {
                _ = new MailAddress(NewStudent.Email);
            }
            catch (FormatException)
            {
                Message = "Please enter a valid email address.";
                await LoadStudentsAsync();
                return Page();
            }

            try
            {
                using var connection = GetConnection();
                await connection.OpenAsync();

                string query = @"
                    INSERT INTO Students (Name, Age, Course, Email)
                    VALUES (@Name, @Age, @Course, @Email)";

                using var command = new MySqlCommand(query, connection);

                command.Parameters.AddWithValue(
                    "@Name", NewStudent.Name.Trim());

                command.Parameters.AddWithValue(
                    "@Age", NewStudent.Age);

                command.Parameters.AddWithValue(
                    "@Course", NewStudent.Course.Trim());

                command.Parameters.AddWithValue(
                    "@Email", NewStudent.Email.Trim());

                await command.ExecuteNonQueryAsync();

                return RedirectToPage();
            }
            catch (MySqlException)
            {
                Message = "Unable to save the student. Please check the database connection.";
                await LoadStudentsAsync();
                return Page();
            }
        }

        // Delete Student
        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                using var connection = GetConnection();
                await connection.OpenAsync();

                string query = "DELETE FROM Students WHERE Id = @Id";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@Id", id);

                await command.ExecuteNonQueryAsync();

                return RedirectToPage();
            }
            catch (MySqlException)
            {
                Message = "Unable to delete the student.";
                await LoadStudentsAsync();
                return Page();
            }
        }

        // Load statistics, course list and filtered students
        private async Task LoadStudentsAsync()
        {
            using var connection = GetConnection();
            await connection.OpenAsync();

            // Load dashboard statistics from all students
            string statsQuery = @"
                SELECT
                    COUNT(*) AS TotalStudents,
                    COUNT(DISTINCT NULLIF(TRIM(Course), '')) AS TotalCourses
                FROM Students";

            using (var statsCommand = new MySqlCommand(statsQuery, connection))
            using (var statsReader = await statsCommand.ExecuteReaderAsync())
            {
                if (await statsReader.ReadAsync())
                {
                    TotalStudents = Convert.ToInt32(
                        statsReader["TotalStudents"]);

                    TotalCourses = Convert.ToInt32(
                        statsReader["TotalCourses"]);
                }
            }

            // Load course names for the filter dropdown
            Courses = new List<string>();

            string coursesQuery = @"
                SELECT DISTINCT TRIM(Course) AS Course
                FROM Students
                WHERE Course IS NOT NULL AND TRIM(Course) <> ''
                ORDER BY Course";

            using (var coursesCommand = new MySqlCommand(coursesQuery, connection))
            using (var coursesReader = await coursesCommand.ExecuteReaderAsync())
            {
                while (await coursesReader.ReadAsync())
                {
                    Courses.Add(coursesReader.GetString(
                        coursesReader.GetOrdinal("Course")));
                }
            }

            // Load students matching search and course filter
            Students = new List<Student>();

            string studentQuery = @"
                SELECT Id, Name, Age, Course, Email
                FROM Students
                WHERE (@SearchName = ''
                       OR Name LIKE CONCAT('%', @SearchName, '%'))
                  AND (@FilterCourse = ''
                       OR LOWER(TRIM(Course)) = LOWER(TRIM(@FilterCourse)))
                ORDER BY Id DESC";

            using var studentCommand = new MySqlCommand(
                studentQuery, connection);

            studentCommand.Parameters.AddWithValue(
                "@SearchName", SearchName?.Trim() ?? string.Empty);

            studentCommand.Parameters.AddWithValue(
                "@FilterCourse", FilterCourse?.Trim() ?? string.Empty);

            using var reader = await studentCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                Students.Add(new Student
                {
                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Age = reader.GetInt32(reader.GetOrdinal("Age")),
                    Course = reader.GetString(reader.GetOrdinal("Course")),
                    Email = reader.GetString(reader.GetOrdinal("Email"))
                });
            }
        }
    }

    public class Student
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Age { get; set; }

        public string Course { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
    }
}
