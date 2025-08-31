using NCalc;
using NCalc.Exceptions;
using NCalc.Handlers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Tinkwell.Firmwareless.Expressions;

public sealed class ExpressionEvaluator
{
    public static readonly object Undefined = new object();

    public object? Evaluate(string expression, object? parameters)
    {
        var expr = new Expression(expression);
        expr.EvaluateFunction += OnEvaluateFunction;
        expr.EvaluateParameter += (name, args) => OnEvaluateParameter(expr, name, args);
        expr.CultureInfo = CultureInfo.InvariantCulture;

        ImportParameters(parameters, expr);

        try
        {
            return expr.Evaluate();
        }
        catch (NCalcFunctionNotFoundException e)
        {
            throw new InvalidExpressionException($"Function {e.FunctionName}() does not exist.");
        }
        catch (NCalcParameterNotDefinedException e)
        {
            throw new InvalidExpressionException($"Parameter '{e.ParameterName}' is not defined.");
        }
        catch (NCalcException e)
        {
            throw new InvalidExpressionException($"Error evaluating an expression: {e.Message}", e);
        }
    }

    public string EvaluateString(string expression, object? parameters)
        => Convert.ToString(Evaluate(expression, parameters), CultureInfo.InvariantCulture) ?? "";

    public bool EvaluateBool(string expression, object? parameters)
        => EvaluateTo(expression, parameters, value => Convert.ToBoolean(value, CultureInfo.InvariantCulture));

    public double EvaluateDoble(string expression, object? parameters)
        => EvaluateTo(expression, parameters, value => Convert.ToDouble(value, CultureInfo.InvariantCulture));

    public bool TryEvaluateBool(string expression, object? parameters, out bool result)
        => TryEvaluateTo(expression, parameters, value => Convert.ToBoolean(value, CultureInfo.InvariantCulture), out result);

    public bool TryEvaluateDouble(string expression, object? parameters, out double result)
        => TryEvaluateTo(expression, parameters, value => Convert.ToDouble(value, CultureInfo.InvariantCulture), out result);

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    private readonly static Dictionary<string, ICustomFunction> _customFunctions =
        typeof(ExpressionEvaluator).Assembly.GetTypes()
            .Where(x => typeof(ICustomFunction).IsAssignableFrom(x) && !x.IsAbstract)
            .Select(x => (ICustomFunction)Activator.CreateInstance(x)!)
            .ToDictionary(x => x.Name, x => x);

    private T EvaluateTo<T>(string expression, object? parameters, Func<object?, T> convert)
    {
        object? value = Evaluate(expression, parameters);
        if (value is null)
            throw new InvalidExpressionException($"Cannot convert a null value to '{typeof(T).Name}'.");

        try
        {
            return convert(value);
        }
        catch (FormatException e)
        {
            throw new InvalidExpressionException($"Cannot convert '{value}' ('{value?.GetType().Name ?? "n/a"}') to '{typeof(T).Name}': {e.Message}", e);
        }
        catch (InvalidCastException e)
        {
            throw new InvalidExpressionException($"Cannot convert '{value}' ('{value?.GetType().Name ?? "n/a"}') to '{typeof(T).Name}': {e.Message}", e);
        }
    }

    private bool TryEvaluateTo<T>(string expression, object? parameters, Func<object?, T> convert, out T result)
    {
        result = default!;

        try
        {
            object? value = Evaluate(expression, parameters);
            if (value is null)
                return false;

            result = convert(value);
            return true;
        }
        catch (Exception e) when (e is not InvalidExpressionException)
        {
            return false;
        }
    }

    private static void ImportParameters(object? parameters, Expression expr)
    {
        if (parameters is not null)
        {
            if (parameters is System.Collections.IDictionary dictionary)
                ImportParametersFromDictionary("", dictionary, expr);
            else
                ImportParametersFromObject(parameters, expr);
        }
    }

    private static void ImportParametersFromDictionary(string prefix, System.Collections.IDictionary parameters, Expression expr)
    {
        foreach (System.Collections.DictionaryEntry kvp in parameters)
        {
            if (ReferenceEquals(kvp.Value, Undefined))
                continue;

            expr.Parameters[prefix + Convert.ToString(kvp.Key, CultureInfo.InvariantCulture)!] = kvp.Value;
        }
    }

    private static void ImportParametersFromObject(object parameters, Expression expr)
    {
        foreach (var property in parameters.GetType().GetProperties())
        {
            var value = property.GetValue(parameters);
            if (ReferenceEquals(value, Undefined))
                continue;

            if (value is System.Collections.IDictionary dictionary)
                ImportParametersFromDictionary($"{property.Name}.", dictionary, expr);
            else
                expr.Parameters[property.Name] = value;
        }
    }

    private void OnEvaluateParameter(Expression expr, string name, ParameterArgs args)
    {
        if (args.Result is not null)
            return;

        var parts = name.Split('.');
        if (parts.Length <= 1)
            return;

        object? currentObject;
        if (expr.Parameters.TryGetValue(parts[0], out var rootObject))
            currentObject = rootObject;
        else
            return;

        for (int i = 1; i < parts.Length; i++)
        {
            // Skipping null objects what we're implementing is basically the null-conditional operator
            if (currentObject is null || ReferenceEquals(currentObject, Undefined))
                return;

            var propertyInfo = currentObject.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo is null)
                return;

            currentObject = propertyInfo.GetValue(currentObject);
        }

        args.Result = currentObject;
    }

    private void OnEvaluateFunction(string name, FunctionArgs args)
    {
        if (!_customFunctions.TryGetValue(name, out var function))
            return;

        var result = function.Call(args);
        if (ReferenceEquals(result, Undefined))
            return;

        args.Result = result;
    }
}
