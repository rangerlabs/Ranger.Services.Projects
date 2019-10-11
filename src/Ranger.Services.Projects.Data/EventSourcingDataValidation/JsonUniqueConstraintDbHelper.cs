using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json.Linq;
using Ranger.Common;

namespace Ranger.Services.Projects.Data
{
    public static class JsonUniqueConstraintDbHelper
    {

        public static (bool isValid, IEnumerable<string> errors) ValidateAgainstDbContext<TDbContext>(this IEnumerable<EntityEntry> entries, TDbContext context)
            where TDbContext : DbContext
        {
            List<string> errors = new List<string>();
            foreach (var entry in entries)
            {
                var entryType = entry.Entity.GetType();
                if (entryType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventStreamDbSet<>)))
                {
                    var jsonDataType = entryType.GenericTypeArguments.Single();
                    foreach (var property in jsonDataType.GetProperties())
                    {
                        if (property.CustomAttributes.Any(ca => ca.AttributeType == typeof(JsonUniqueConstraintAttribute)))
                        {
                            var jsonValue = JToken.Parse(entry.CurrentValues.GetValue<string>("Data")).Value<string>(property.Name);
                            using (var command = context.Database.GetDbConnection().CreateCommand())
                            {
                                command.CommandType = System.Data.CommandType.Text;
                                command.CommandText = $"select exists (select * from @table_name where data ->> '@property_name' = '@current_value'";

                                var tableName = command.CreateParameter();
                                tableName.ParameterName = "@table_name";
                                tableName.Value = entry.Metadata.Name;

                                var propertyName = command.CreateParameter();
                                propertyName.ParameterName = "@property_name";
                                propertyName.Value = property.Name;

                                var currentValue = command.CreateParameter();
                                currentValue.ParameterName = "@current_value";
                                currentValue.Value = jsonValue;

                                command.Parameters.Add(tableName);
                                command.Parameters.Add(propertyName);
                                command.Parameters.Add(currentValue);

                                var reader = command.ExecuteReader();
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        errors.Add($"Property with name '{property.Name}' on Entity '{entryType.Name}' has non-unique value '{jsonValue}'.");
                                    }
                                }
                                else
                                {
                                    throw new Exception($"No rows returned validating JSON property '{property.Name}' uniqueness.");
                                }
                                reader.Close();
                            }
                        }
                    }
                }
            }
            return (errors.Count == 0, errors);
        }
    }
}
