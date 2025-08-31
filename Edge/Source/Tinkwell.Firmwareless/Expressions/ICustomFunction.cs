namespace Tinkwell.Firmwareless.Expressions;

interface ICustomFunction
{
    public string Name { get; }
    object? Call(NCalc.Handlers.FunctionArgs args);
}