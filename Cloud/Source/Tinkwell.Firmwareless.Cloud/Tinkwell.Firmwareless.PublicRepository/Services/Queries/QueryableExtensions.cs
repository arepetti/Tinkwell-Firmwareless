using System.Linq.Expressions;
using System.Reflection;

namespace Tinkwell.Firmwareless.PublicRepository.Services.Queries;

static class QueryableExtensions
{
    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> source, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return source;

        return ApplyFilters(source, FilterParser.Parse(filter));
    }

    public static IQueryable<T> ApplySorting<T>(this IQueryable<T> source, string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return source;

        var sortParams = sort
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool first = true;

        foreach (var param in sortParams)
        {
            bool descending = param.StartsWith("-");
            var propertyName = descending ? param[1..] : param;

            source = source.OrderByProperty(propertyName, descending, first);
            first = false;
        }

        return source;
    }

    private static IQueryable<T> ApplyFilters<T>(this IQueryable<T> source, IEnumerable<FilterTerm> terms)
    {
        if (terms is null || !terms.Any())
            return source;

        var param = Expression.Parameter(typeof(T), "x");
        Expression? body = null;

        foreach (var term in terms)
        {
            Expression prop = param;
            foreach (var segment in term.Field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                prop = Expression.PropertyOrField(prop, segment);

            Expression termExpr = term.Op switch
            {
                FilterOp.Eq => BuildEquals(prop, term.Value),
                FilterOp.Contains => BuildContainsIgnoreCase(prop, term.Value),
                _ => throw new NotSupportedException()
            };

            body = body is null ? termExpr : Expression.AndAlso(body, termExpr);
        }

        if (body is null)
            return source;

        var lambda = Expression.Lambda<Func<T, bool>>(body, param);
        return source.Where(lambda);
    }

    private static BinaryExpression BuildEquals(Expression prop, string value)
    {
        var constant = ToConstant(prop.Type, value);
        return Expression.Equal(prop, constant);
    }

    private static BinaryExpression BuildContainsIgnoreCase(Expression prop, string value)
    {
        if (prop.Type != typeof(string))
            throw new NotSupportedException($"Contains is supported only on string properties. Property type: {prop.Type.Name}");

        var notNull = Expression.NotEqual(prop, Expression.Constant(null, typeof(string)));

        var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        var propLower = Expression.Call(prop, toLower);
        var valueLower = Expression.Constant(value.ToLowerInvariant());
        var containsCall = Expression.Call(propLower, contains, valueLower);

        return Expression.AndAlso(notNull, containsCall);
    }

    private static Expression ToConstant(Type targetType, string raw)
    {
        if (targetType == typeof(string))
            return Expression.Constant(raw, typeof(string));

        if (targetType.IsEnum)
            return Expression.Constant(Enum.Parse(targetType, raw, ignoreCase: true), targetType);

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var converted = Convert.ChangeType(raw, underlying, System.Globalization.CultureInfo.InvariantCulture);
        return Expression.Convert(Expression.Constant(converted, underlying), targetType);
    }


    private static IQueryable<T> OrderByProperty<T>(
        this IQueryable<T> source, string propertyName, bool descending, bool first)
    {
        var entityType = typeof(T);
        var property = entityType.GetProperty(
            propertyName,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (property == null)
            throw new ArgumentException($"No property '{propertyName}' found on {entityType.Name}");

        var parameter = Expression.Parameter(entityType, "x");
        var propertyAccess = Expression.Property(parameter, property);
        var orderByExp = Expression.Lambda(propertyAccess, parameter);

        string methodName;
        if (first)
            methodName = descending ? nameof(Enumerable.OrderByDescending) : nameof(Enumerable.OrderBy);
        else
            methodName = descending ? nameof(Enumerable.ThenByDescending) : nameof(Enumerable.ThenBy);

        var resultExp = Expression.Call(
            typeof(Queryable),
            methodName,
            [entityType, property.PropertyType],
            source.Expression,
            Expression.Quote(orderByExp));

        return source.Provider.CreateQuery<T>(resultExp);
    }
}
