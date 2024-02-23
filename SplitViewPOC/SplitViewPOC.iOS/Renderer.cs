using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using CoreGraphics;
using Foundation;
using UIKit;

namespace Xamarin.Forms.Platform.iOS
{
	internal class ChildViewController : UIViewController
	{
		public override void ViewDidLayoutSubviews()
		{
			foreach (var vc in ChildViewControllers)
				vc.View.Frame = View.Bounds;
		}
	}

	internal class EventedViewController : ChildViewController
	{
		FlyoutView _flyoutView;

		event EventHandler _didAppear;
		event EventHandler _willDisappear;

		public EventedViewController()
		{
			_flyoutView = new FlyoutView();
		}


		public event EventHandler DidAppear
		{
			add
			{
				_flyoutView.DidAppear += value;
				_didAppear += value;
			}
			remove
			{
				_flyoutView.DidAppear -= value;
				_didAppear -= value;
			}
		}

		public event EventHandler WillDisappear
		{
			add
			{
				_flyoutView.WillDisappear += value;
				_willDisappear += value;
			}
			remove
			{
				_flyoutView.WillDisappear -= value;
				_willDisappear -= value;
			}
		}

		public override void ViewDidAppear(bool animated)
		{
			base.ViewDidAppear(animated);
			_didAppear?.Invoke(this, EventArgs.Empty);
		}

		public override void ViewWillDisappear(bool animated)
		{
			base.ViewWillDisappear(animated);
			_willDisappear?.Invoke(this, EventArgs.Empty);
		}

		public override void ViewDidDisappear(bool animated)
		{
			base.ViewDidDisappear(animated);
			_willDisappear?.Invoke(this, EventArgs.Empty);
		}

		public override void LoadView()
		{
			View = _flyoutView;
		}

		public class FlyoutView : UIView
		{
			public bool IsCollapsed => Center.X <= 0;
			bool _previousIsCollapsed = true;

			public event EventHandler DidAppear;
			public event EventHandler WillDisappear;

			// this only gets called on iOS12 everytime it's collapsed or expanded
			// I haven't found an override on iOS13 that gets called but it doesn't seem
			// to matter because the DidAppear and WillDisappear seem more consistent on iOS 13
			public override void LayoutSubviews()
			{
				base.LayoutSubviews();
				UpdateCollapsedSetting();
			}

			void UpdateCollapsedSetting()
			{
				if (_previousIsCollapsed != IsCollapsed)
				{
					_previousIsCollapsed = IsCollapsed;

					if (IsCollapsed)
						WillDisappear?.Invoke(this, EventArgs.Empty);
					else
						DidAppear?.Invoke(this, EventArgs.Empty);
				}
			}
		}
	}

	public class TabletFlyoutPageRenderer : UISplitViewController, IVisualElementRenderer, IEffectControlProvider
	{
		private const string XamarinRenderEvent = "Xamarin.UpdateToolbarButtons";
		UIViewController _detailController;

		bool _disposed;
		EventTracker _events;
		InnerDelegate _innerDelegate;
		nfloat _flyoutWidth = 0;
		EventedViewController _flyoutController;
		FlyoutPage _flyoutPage;
		VisualElementTracker _tracker;
		CGSize _previousSize = CGSize.Empty;
		CGSize _previousViewDidLayoutSize = CGSize.Empty;
		UISplitViewControllerDisplayMode _previousDisplayMode = UISplitViewControllerDisplayMode.Automatic;
		private bool _isSmallWindowPresented;
		private bool _hasAppFullyStarted;
		Page PageController => Element as Page;
		Element ElementController => Element as Element;
		bool IsFlyoutVisible => !(_flyoutController?.View as EventedViewController.FlyoutView).IsCollapsed;

		protected FlyoutPage FlyoutPage => _flyoutPage ?? (_flyoutPage = (FlyoutPage)Element);

		[Internals.Preserve(Conditional = true)]
		public TabletFlyoutPageRenderer()
		{

		}

		protected override void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			if (disposing)
			{
				if (Element != null)
				{
					PageController.SendDisappearing();
					Element.PropertyChanged -= HandlePropertyChanged;

					if (FlyoutPage?.Flyout != null)
					{
						FlyoutPage.Flyout.PropertyChanged -= HandleFlyoutPropertyChanged;
					}

					Element = null;
				}

				if (_tracker != null)
				{
					_tracker.Dispose();
					_tracker = null;
				}

				if (_events != null)
				{
					_events.Dispose();
					_events = null;
				}

				if (_flyoutController != null)
				{
					_flyoutController.DidAppear -= FlyoutControllerDidAppear;
					_flyoutController.WillDisappear -= FlyoutControllerWillDisappear;
					FlyoutPage.IsPresentedChanged -= FlyoutPage_IsPresentedChanged;
				}

				ClearControllers();
			}

			base.Dispose(disposing);
		}

		public VisualElement Element { get; private set; }

		public event EventHandler<VisualElementChangedEventArgs> ElementChanged;

		public SizeRequest GetDesiredSize(double widthConstraint, double heightConstraint)
		{
			return NativeView.GetSizeRequest(widthConstraint, heightConstraint);
		}

		public UIView NativeView
		{
			get { return View; }
		}

		public void SetElement(VisualElement element)
		{
			var oldElement = Element;
			Element = element;

			ViewControllers = new[] { _flyoutController = new EventedViewController(), _detailController = new ChildViewController() };

			UpdateControllers();

			_flyoutController.DidAppear += FlyoutControllerDidAppear;
			_flyoutController.WillDisappear += FlyoutControllerWillDisappear;
			FlyoutPage.IsPresentedChanged += FlyoutPage_IsPresentedChanged;

			PresentsWithGesture = FlyoutPage.IsGestureEnabled;
			OnElementChanged(new VisualElementChangedEventArgs(oldElement, element));

			RegisterEffectControlProvider(this, oldElement, element);

			// I can't fake this bit... not sure what to do here
			//if (element != null)
			//	element.SendViewInitialized(NativeView);
		}

		private static void RegisterEffectControlProvider(IEffectControlProvider self, IElementController oldElement, IElementController newElement)
		{
			IElementController controller = oldElement;
			if (controller != null && controller.EffectControlProvider == self)
				controller.EffectControlProvider = null;

			controller = newElement;
			if (controller != null)
				controller.EffectControlProvider = self;
		}

		public void SetElementSize(Size size)
		{
			Element.Layout(new Rectangle(Element.X, Element.Width, size.Width, size.Height));
		}

		public UIViewController ViewController
		{
			get { return this; }
		}

		public override void ViewDidAppear(bool animated)
		{
			PageController.SendAppearing();
			base.ViewDidAppear(animated);
			ToggleFlyout();
		}

		public override void ViewDidDisappear(bool animated)
		{
			base.ViewDidDisappear(animated);
			PageController?.SendDisappearing();
		}

		public override void ViewDidLayoutSubviews()
		{
			base.ViewDidLayoutSubviews();

			bool layoutFlyout = false;
			bool layoutDetails = false;

			layoutFlyout = _flyoutController?.View?.Superview != null;
			layoutDetails = _detailController?.View?.Superview != null;
			var flyoutSuperView = _flyoutController.View.Superview?.ToString();
			var detailSuperView = _detailController.View.Superview?.ToString();
			bool isSmallWindow = IsSmallWindow(View.Bounds.Size);
			if (isSmallWindow && !_isSmallWindowPresented && !layoutDetails)
			{
				//var detail = Platform.GetRenderer(FlyoutPage.Detail).ViewController;
				//_detailController.View.AddSubview(detail.View);
				//_detailController.AddChildViewController(detail);
				View.AddSubview(_detailController.View);
				//_flyoutController.View.Superview.AddSubview(_detailController.View);
				layoutDetails = true;
				//PerformButtonSelector();

			}
			if (!isSmallWindow
				&& !layoutFlyout
				&& _flyoutController.View.Superview is null
				&& IsBeingPresented)
			{
				//_detailController.View.RemoveFromSuperview();
				View.AddSubview(_flyoutController.View);
				layoutFlyout = true;
			}

			if (layoutFlyout)
			{
				var flyoutBounds = _flyoutController.View.Frame;

				_flyoutWidth = flyoutBounds.Width;
				if (isSmallWindow && !_isSmallWindowPresented)
				{
					_flyoutWidth = 0;
				}

				if (!flyoutBounds.IsEmpty)
					FlyoutPage.FlyoutBounds = new Rectangle(0, 0, _flyoutWidth, flyoutBounds.Height);
			}

			if (layoutDetails)
			{
				var detailsBounds = _detailController.View.Frame;
				if (isSmallWindow && !_isSmallWindowPresented)
				{
					detailsBounds.Width = View.Bounds.Width;
				}
				if (!detailsBounds.IsEmpty)
					FlyoutPage.DetailBounds = new Rectangle(0, 0, detailsBounds.Width, detailsBounds.Height);
			}

			if (_previousViewDidLayoutSize == CGSize.Empty)
				_previousViewDidLayoutSize = View.Bounds.Size;

			// Is this being called from a rotation
			if (_previousViewDidLayoutSize != View.Bounds.Size)
			{
				_previousViewDidLayoutSize = View.Bounds.Size;

				// make sure IsPresented matches state of Flyout View
				if (FlyoutPage.CanChangeIsPresented && FlyoutPage.IsPresented != IsFlyoutVisible)
					ElementController.SetValueFromRenderer(Xamarin.Forms.FlyoutPage.IsPresentedProperty, IsFlyoutVisible);
			}

			if (_previousDisplayMode != PreferredDisplayMode)
			{
				_previousDisplayMode = PreferredDisplayMode;

				// make sure IsPresented matches state of Flyout View
				if (FlyoutPage.CanChangeIsPresented && FlyoutPage.IsPresented != IsFlyoutVisible)
					ElementController.SetValueFromRenderer(Xamarin.Forms.FlyoutPage.IsPresentedProperty, IsFlyoutVisible);
			}
		}

		private void FlyoutPage_IsPresentedChanged(object sender, EventArgs e)
		{
			if (IsSmallWindow(View.Bounds.Size))
			{
				if (!_hasAppFullyStarted || View.Subviews.Length < 2 || View.Subviews.Contains(_flyoutController.View))
				{
					_flyoutController.View.RemoveFromSuperview();
					View.AddSubview(_detailController.View);
					_isSmallWindowPresented = false;
					_hasAppFullyStarted = true;
				}
				else
				{
					_detailController.View.RemoveFromSuperview();
					View.AddSubview(_flyoutController.View);
					_isSmallWindowPresented = true;
				}
			}
		}

		private bool IsSmallWindow(CGSize bounds) => bounds.Width <= 375;

		public override void ViewDidLoad()
		{
			base.ViewDidLoad();
			UpdateBackground();
			UpdateFlowDirection();
			UpdateFlyoutLayoutBehavior(View.Bounds.Size);
			_tracker = new VisualElementTracker(this);
			_events = new EventTracker(this);
			_events.LoadEvents(NativeView);
		}

		public void PokeStuff()
		{
			UpdateFlyoutLayoutBehavior(View.Bounds.Size);
		}

		void UpdateFlyoutLayoutBehavior(CGSize newBounds)
		{
			FlyoutPage flyoutDetailPage = _flyoutPage ?? Element as FlyoutPage;

			if (flyoutDetailPage == null)
				return;

			bool isPortrait = newBounds.Height > newBounds.Width;
			var isSmallWindow = IsSmallWindow(newBounds);
			var previous = PreferredDisplayMode;

			Debug.WriteLine($"Display Mode: {DisplayMode} Primary Width: {PrimaryColumnWidth} Collapsed: {Collapsed} Is Being Presented: {IsBeingPresented}");
			switch (flyoutDetailPage.FlyoutLayoutBehavior)
			{
				case FlyoutLayoutBehavior.Split:
					PreferredDisplayMode = UISplitViewControllerDisplayMode.OneBesideSecondary;
					break;
				case FlyoutLayoutBehavior.Popover:
					PreferredDisplayMode = UISplitViewControllerDisplayMode.SecondaryOnly;
					break;
				case FlyoutLayoutBehavior.SplitOnPortrait:
					PreferredDisplayMode = (isPortrait) ? UISplitViewControllerDisplayMode.OneBesideSecondary : UISplitViewControllerDisplayMode.SecondaryOnly;
					break;
				case FlyoutLayoutBehavior.SplitOnLandscape:
					PreferredDisplayMode = (!isPortrait) ? UISplitViewControllerDisplayMode.OneBesideSecondary : UISplitViewControllerDisplayMode.OneBesideSecondary;
					break;
				default:
					PreferredDisplayMode = UISplitViewControllerDisplayMode.Automatic;
					break;
			}

			Debug.WriteLine($"Display Mode: {DisplayMode} Primary Width: {PrimaryColumnWidth} Collapsed: {Collapsed} Is Being Presented: {IsBeingPresented}");

			if (previous == PreferredDisplayMode)
			{
				return;
			}

			Debug.WriteLine($"Display Mode: {DisplayMode} Primary Width: {PrimaryColumnWidth} Collapsed: {Collapsed} Is Being Presented: {IsBeingPresented}");
			if (!FlyoutPage.ShouldShowSplitMode)
				FlyoutPage.CanChangeIsPresented = true;

			FlyoutPage.UpdateFlyoutLayoutBehavior();
		}

		public override void ViewWillDisappear(bool animated)
		{
			if (IsFlyoutVisible && !FlyoutPage.ShouldShowSplitMode)
				PerformButtonSelector();

			base.ViewWillDisappear(animated);
		}

		public override void ViewWillLayoutSubviews()
		{
			base.ViewWillLayoutSubviews();
			_flyoutController.View.BackgroundColor = UIColor.SystemBackground;
		}

		public override void WillRotate(UIInterfaceOrientation toInterfaceOrientation, double duration)
		{
			base.WillRotate(toInterfaceOrientation, duration);
		}

		public override UIViewController ChildViewControllerForStatusBarHidden()
		{
			if (((FlyoutPage)Element).Detail != null)
				return (UIViewController)Platform.GetRenderer(((FlyoutPage)Element).Detail);
			else
				return base.ChildViewControllerForStatusBarHidden();
		}

		public override UIViewController ChildViewControllerForHomeIndicatorAutoHidden
		{
			get
			{
				if (((FlyoutPage)Element).Detail != null)
					return (UIViewController)Platform.GetRenderer(((FlyoutPage)Element).Detail);
				else
					return base.ChildViewControllerForHomeIndicatorAutoHidden;
			}
		}

		protected virtual void OnElementChanged(VisualElementChangedEventArgs e)
		{
			if (e.OldElement != null)
				e.OldElement.PropertyChanged -= HandlePropertyChanged;

			if (e.NewElement != null)
				e.NewElement.PropertyChanged += HandlePropertyChanged;

			var changed = ElementChanged;
			if (changed != null)
				changed(this, e);

			_flyoutWidth = 0;
		}

		void ClearControllers()
		{
			foreach (var controller in _flyoutController.ChildViewControllers)
			{
				controller.View.RemoveFromSuperview();
				controller.RemoveFromParentViewController();
			}

			foreach (var controller in _detailController.ChildViewControllers)
			{
				controller.View.RemoveFromSuperview();
				controller.RemoveFromParentViewController();
			}
		}

		void HandleFlyoutPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == Page.IconImageSourceProperty.PropertyName || e.PropertyName == Page.TitleProperty.PropertyName)
				MessagingCenter.Send<IVisualElementRenderer>(this, XamarinRenderEvent);
		}

		void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (_tracker == null)
				return;

			if (e.PropertyName == "Flyout" || e.PropertyName == "Detail")
				UpdateControllers();
			else if (e.PropertyName == Xamarin.Forms.FlyoutPage.IsPresentedProperty.PropertyName)
				ToggleFlyout();
			else if (e.PropertyName == Xamarin.Forms.FlyoutPage.IsGestureEnabledProperty.PropertyName)
				base.PresentsWithGesture = this.FlyoutPage.IsGestureEnabled;
			else if (e.PropertyName == VisualElement.BackgroundColorProperty.PropertyName || e.PropertyName == VisualElement.BackgroundProperty.PropertyName)
				UpdateBackground();
			else if (e.PropertyName == VisualElement.FlowDirectionProperty.PropertyName)
				UpdateFlowDirection();
			else if (e.Is(Xamarin.Forms.FlyoutPage.FlyoutLayoutBehaviorProperty))
				UpdateFlyoutLayoutBehavior(base.View.Bounds.Size);

			MessagingCenter.Send<IVisualElementRenderer>(this, XamarinRenderEvent);
		}

		public override void ViewWillTransitionToSize(CGSize toSize, IUIViewControllerTransitionCoordinator coordinator)
		{
			base.ViewWillTransitionToSize(toSize, coordinator);

			if (_previousSize != toSize)
			{
				_previousSize = toSize;
				UpdateFlyoutLayoutBehavior(toSize);
			}
		}

		void FlyoutControllerDidAppear(object sender, EventArgs e)
		{
			if (FlyoutPage.CanChangeIsPresented && IsFlyoutVisible)
				ElementController.SetValueFromRenderer(Xamarin.Forms.FlyoutPage.IsPresentedProperty, true);
		}

		void FlyoutControllerWillDisappear(object sender, EventArgs e)
		{
			if (FlyoutPage.CanChangeIsPresented && !IsFlyoutVisible)
				ElementController.SetValueFromRenderer(Xamarin.Forms.FlyoutPage.IsPresentedProperty, false);
		}

		void PerformButtonSelector()
		{
			DisplayModeButtonItem.Target.PerformSelector(DisplayModeButtonItem.Action, DisplayModeButtonItem, 0);
		}

		void ToggleFlyout()
		{
			if (IsFlyoutVisible == FlyoutPage.IsPresented || FlyoutPage.ShouldShowSplitMode)
				return;

			PerformButtonSelector();
		}

		void UpdateBackground()
		{
			_ = this.ApplyNativeImageAsync(Page.BackgroundImageSourceProperty, bgImage =>
			{
				if (bgImage != null)
					View.BackgroundColor = UIColor.FromPatternImage(bgImage);
				else
				{
					Brush background = Element.Background;

					if (!Brush.IsNullOrEmpty(background))
						View.UpdateBackground(Element.Background);
					else
					{
						if (Element.BackgroundColor == Color.Default)
							View.BackgroundColor = UIColor.White;
						else
							View.BackgroundColor = Element.BackgroundColor.ToUIColor();
					}
				}
			});
		}

		void UpdateControllers()
		{
			FlyoutPage.Flyout.PropertyChanged -= HandleFlyoutPropertyChanged;

			if (Platform.GetRenderer(FlyoutPage.Flyout) == null)
				Platform.SetRenderer(FlyoutPage.Flyout, Platform.CreateRenderer(FlyoutPage.Flyout));
			if (Platform.GetRenderer(FlyoutPage.Detail) == null)
				Platform.SetRenderer(FlyoutPage.Detail, Platform.CreateRenderer(FlyoutPage.Detail));

			ClearControllers();

			FlyoutPage.Flyout.PropertyChanged += HandleFlyoutPropertyChanged;

			var flyout = Platform.GetRenderer(FlyoutPage.Flyout).ViewController;
			var detail = Platform.GetRenderer(FlyoutPage.Detail).ViewController;

			_flyoutController.View.AddSubview(flyout.View);
			_flyoutController.AddChildViewController(flyout);

			_detailController.View.AddSubview(detail.View);
			_detailController.AddChildViewController(detail);
		}

		void UpdateFlowDirection()
		{
			if (NativeView.UpdateFlowDirection(Element)
			&& NativeView.Superview != null)
			{
				var view = NativeView.Superview;
				NativeView.RemoveFromSuperview();
				view.AddSubview(NativeView);
			}
		}

		class InnerDelegate : UISplitViewControllerDelegate
		{
			readonly FlyoutLayoutBehavior _flyoutPresentedDefaultState;

			public InnerDelegate(FlyoutLayoutBehavior flyoutPresentedDefaultState)
			{
				_flyoutPresentedDefaultState = flyoutPresentedDefaultState;
			}

			public override bool ShouldHideViewController(UISplitViewController svc, UIViewController viewController, UIInterfaceOrientation inOrientation)
			{
				bool willHideViewController;
				switch (_flyoutPresentedDefaultState)
				{
					case FlyoutLayoutBehavior.Split:
						willHideViewController = false;
						break;
					case FlyoutLayoutBehavior.Popover:
						willHideViewController = true;
						break;
					case FlyoutLayoutBehavior.SplitOnPortrait:
						willHideViewController = !(inOrientation == UIInterfaceOrientation.Portrait || inOrientation == UIInterfaceOrientation.PortraitUpsideDown);
						break;
					default:
						willHideViewController = inOrientation == UIInterfaceOrientation.Portrait || inOrientation == UIInterfaceOrientation.PortraitUpsideDown;
						break;
				}
				return willHideViewController;
			}
		}

		void IEffectControlProvider.RegisterEffect(Effect effect)
		{
			VisualElementRenderer<VisualElement>.RegisterEffect(effect, View);
		}
	}
}