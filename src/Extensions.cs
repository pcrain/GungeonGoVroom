namespace GGV;

internal static class Extensions
{
    /// <summary>Convenience method for calling an internal / private static function with an ILCursor</summary>
    public static void CallPrivate(this ILCursor cursor, Type t, string name)
    {
        cursor.Emit(OpCodes.Call, t.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
    }

    /// <summary>Declare a local variable in an ILManipulator</summary>
    public static VariableDefinition DeclareLocal<T>(this ILContext il)
    {
        VariableDefinition v = new VariableDefinition(il.Import(typeof(T)));
        il.Body.Variables.Add(v);
        return v;
    }
}
