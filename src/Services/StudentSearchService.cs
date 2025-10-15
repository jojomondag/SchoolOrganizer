using System;
using System.Collections.Generic;
using System.Linq;
using SchoolOrganizer.Src.Models.Students;

namespace SchoolOrganizer.Src.Services;

public class StudentSearchService
{
    public IEnumerable<Student> Search(IEnumerable<Student> students, string query)
    {
        if (students == null) return Array.Empty<Student>();

        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return students;
        }

        var tokens = trimmed
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant())
            .ToArray();

        // Score: prioritize name starts-with, then name contains, then other fields contains
        return students
            .Select(s => new
            {
                Student = s,
                Score = CalculateScore(s, tokens)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Student.Name)
            .Select(x => x.Student)
            .ToList();
    }

    private static int CalculateScore(Student student, IReadOnlyList<string> tokens)
    {
        int totalScore = 0;

        foreach (var token in tokens)
        {
            int tokenScore = 0;

            // Name scoring (highest priority)
            var name = student.Name ?? string.Empty;
            if (name.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                tokenScore = 6;
            else if (name.Contains(token, StringComparison.OrdinalIgnoreCase))
                tokenScore = 4;

            // Class scoring
            if (tokenScore < 3)
            {
                var cls = student.ClassName ?? string.Empty;
                if (cls.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    tokenScore = 3;
                else if (cls.Contains(token, StringComparison.OrdinalIgnoreCase))
                    tokenScore = 2;
            }

            // Teachers scoring
            if (tokenScore < 2)
            {
                foreach (var teacher in student.Teachers ?? new())
                {
                    if (teacher.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        tokenScore = 2;
                        break;
                    }
                }
            }

            // Email and date scoring (lowest priority)
            if (tokenScore < 1)
            {
                var email = student.Email ?? string.Empty;
                if (email.Contains(token, StringComparison.OrdinalIgnoreCase))
                    tokenScore = 1;
                else
                {
                    var dateStr = student.EnrollmentDate.ToString("yyyy-MM-dd");
                    if (dateStr.Contains(token, StringComparison.OrdinalIgnoreCase))
                        tokenScore = 1;
                }
            }

            if (tokenScore == 0)
                return 0;

            totalScore += tokenScore;
        }

        return totalScore;
    }
}