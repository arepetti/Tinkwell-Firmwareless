namespace Tinkwell.Firmwareless.WamrAotHost.Hosting;

static partial class WamrHost
{
    public static string Signature(Type returnType, params Type[] paramTypes)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('(');
        foreach (var pt in paramTypes)
            sb.Append(TypeChar(pt));
        sb.Append(')');

        if (returnType != typeof(void))
            sb.Append(TypeChar(returnType));

        return sb.ToString();
    }

    private static char TypeChar(Type t)
        => t switch
        {
            var _ when t == typeof(int) => 'i',
            var _ when t == typeof(long) => 'l',
            var _ when t == typeof(float) => 'f',
            var _ when t == typeof(double) => 'd',
            var _ when t == typeof(nint) => 'i', // TODO: if the module is wasm64 then this is "l"
            var _ when t == typeof(void) => 'v',
            _ => throw new NotSupportedException($"Type '{t.FullName}' is not supported in WAMR signatures.")
        };
}
