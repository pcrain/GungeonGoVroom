namespace GGV;

internal static class Extensions
{
    /// <summary>Convenience method for calling an internal / private static function with an ILCursor</summary>
    public static void CallPrivate(this ILCursor cursor, Type t, string name)
    {
        cursor.Emit(OpCodes.Call, t.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
    }
}
