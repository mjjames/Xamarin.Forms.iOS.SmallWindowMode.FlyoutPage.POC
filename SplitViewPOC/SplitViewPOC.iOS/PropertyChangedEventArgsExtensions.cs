using System.ComponentModel;
using System.Runtime.CompilerServices;
using Xamarin.Forms;

internal static class PropertyChangedEventArgsExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool Is(this PropertyChangedEventArgs args, BindableProperty property)
	{
		return args.PropertyName == property.PropertyName;
	}
}