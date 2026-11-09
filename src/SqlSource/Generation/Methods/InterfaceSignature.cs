using System.Text;
using SqlSource.Diagnostics;

namespace SqlSource.Generation.Methods;

/// <summary>
/// Where one type's repository interface goes: what it is compared by with the models, the extension classes and the
/// other interfaces that SqlSource writes.
/// </summary>
/// <param name="TypeKey">The type, as <see cref="TypeMethods.TypeKey" /> gives it.</param>
/// <param name="TypeName">The type in full, as a diagnostic names it: <c>App.Sample&lt;T&gt;</c>.</param>
/// <param name="Name">The interface's name, as a value.</param>
/// <param name="FullName">
/// The interface's full name, from identifier values: the namespace, each type the type is nested in, and the name, as
/// <c>App.IUserRepository</c>.
/// </param>
/// <param name="Arity">The number of the interface's type parameters, which are the type's.</param>
/// <param name="AttributeLocation">Where the type's attribute is.</param>
internal sealed record InterfaceSignature(
    string TypeKey,
    string TypeName,
    string Name,
    string FullName,
    int Arity,
    LocationInfo AttributeLocation
)
{
    /// <summary>The type's interface, or null when it gets none.</summary>
    public static InterfaceSignature? Of(TypeMethods methods)
    {
        if (methods.InterfaceName is not { } name)
        {
            return null;
        }

        var fullName = new StringBuilder(methods.Namespace);
        var last = methods.Types.Count - 1;
        for (var index = 0; index < last; index++)
        {
            _ = (fullName.Length > 0 ? fullName.Append('.') : fullName).Append(methods.Types[index].ValueName);
        }

        _ = (fullName.Length > 0 ? fullName.Append('.') : fullName).Append(name);
        return new InterfaceSignature(
            methods.TypeKey,
            methods.DisplayName,
            name,
            fullName.ToString(),
            methods.Types[last].Arity,
            methods.AttributeLocation
        );
    }
}
