using System.Data.Common;

namespace WordGame.Web.Data;

internal static class WordGameDbCommandExtensions
{
    public static void AddParameter(this DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
