namespace GGV;

internal static class Extensions
{
    /// <summary>Convenience method for calling an internal / private static function with an ILCursor</summary>
    internal static void CallPrivate(this ILCursor cursor, Type t, string name)
    {
        cursor.Emit(OpCodes.Call, t.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
    }

    private static MethodInfo _WriteLine = typeof(System.Console).GetMethod(nameof(System.Console.WriteLine), new Type[]{typeof(string)});
    /// <summary>Convenience method for debug print with an ILCursor</summary>
    [System.Diagnostics.Conditional("DEBUG")]
    internal static void DebugPrint(this ILCursor cursor, string test)
    {
        cursor.Emit(OpCodes.Ldstr, test);
        cursor.Emit(OpCodes.Call, _WriteLine);
    }

    /// <summary>Declare a local variable in an ILManipulator</summary>
    internal static VariableDefinition DeclareLocal<T>(this ILContext il)
    {
        VariableDefinition v = new VariableDefinition(il.Import(typeof(T)));
        il.Body.Variables.Add(v);
        return v;
    }

    /// <summary>Declare a local variable in an ILManipulator</summary>
    internal static VariableDefinition DeclareLocal(this ILContext il, Type t)
    {
        VariableDefinition v = new VariableDefinition(il.Import(t));
        il.Body.Variables.Add(v);
        return v;
    }
}
