using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace QuestionAPI.Helpers;

internal static class SqlServerUniqueViolationClassifier
{
    internal static bool IsUniqueViolation(DbUpdateException exception)
    {
        Exception? current = exception;

        while (current is not null)
        {
            if (current is SqlException sqlException &&
                IsUniqueViolationNumber(sqlException.Number))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    internal static bool IsUniqueViolationNumber(int number)
    {
        return number is 2601 or 2627;
    }
}
