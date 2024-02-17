using SplitViewPOC.ViewModels;
using System.ComponentModel;
using Xamarin.Forms;

namespace SplitViewPOC.Views
{
	public partial class ItemDetailPage : ContentPage
	{
		public ItemDetailPage()
		{
			InitializeComponent();
			BindingContext = new ItemDetailViewModel();
		}
	}
}