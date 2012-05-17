using System;

using Xwt.Backends;

using Gtk;
using Cairo;

namespace Xwt.GtkBackend
{
	public class PopoverBackend : IPopoverBackend
	{
		class PopoverWindow : Gtk.Window
		{
			const int arrowPadding = 20;

			bool supportAlpha;
			Xwt.Popover.Position arrowPosition;
			Gtk.Alignment alignment;

			public PopoverWindow (Gtk.Widget child, Xwt.Popover.Position orientation) : base (WindowType.Toplevel)
			{
				this.AppPaintable = true;
				this.Decorated = false;
				this.SkipPagerHint = true;
				this.SkipTaskbarHint = true;
				this.TypeHint = Gdk.WindowTypeHint.PopupMenu;
				//this.TransientFor = (Gtk.Window)child.Toplevel;
				this.AddEvents ((int)Gdk.EventMask.FocusChangeMask);
				//this.DefaultHeight = this.DefaultWidth = 400;
				this.arrowPosition = orientation;
				this.alignment = SetupAlignment ();
				this.Add (alignment);
				this.alignment.Add (child);
				this.FocusOutEvent += HandleFocusOutEvent;
				OnScreenChanged (null);
			}

			void HandleFocusOutEvent (object o, FocusOutEventArgs args)
			{
				this.HideAll ();
			}

			Gtk.Alignment SetupAlignment ()
			{
				const int defaultPadding = 20;
				var align = new Gtk.Alignment (0, 0, 1, 1);
				align.LeftPadding = align.RightPadding = defaultPadding;
				if (arrowPosition == Xwt.Popover.Position.Top) {
					align.TopPadding = arrowPadding + defaultPadding;
					align.BottomPadding = defaultPadding;
				} else {
					align.BottomPadding = arrowPadding + defaultPadding;
					align.TopPadding = defaultPadding;
				}

				return align;
			}

			protected override void OnScreenChanged (Gdk.Screen previous_screen)
			{
				// To check if the display supports alpha channels, get the colormap
				var colormap = this.Screen.RgbaColormap;
				if (colormap == null) {
					colormap = this.Screen.RgbColormap;
					supportAlpha = false;
				} else {
					supportAlpha = true;
				}
				this.Colormap = colormap;
				base.OnScreenChanged (previous_screen);
			}

			protected override bool OnExposeEvent (Gdk.EventExpose evnt)
			{
				int w, h;
				this.GdkWindow.GetSize (out w, out h);
				var bounds = new Xwt.Rectangle (5, 5, w - 5, h - 5);
				var backgroundColor = Xwt.Drawing.Color.FromBytes (230, 230, 230, 230);
				var black = Xwt.Drawing.Colors.Black;

				using (Context ctx = Gdk.CairoHelper.Create (this.GdkWindow)) {
					// We clear the surface with a transparent color if possible
					if (supportAlpha)
						ctx.Color = new Color (1.0, 1.0, 1.0, 0.0);
					else
						ctx.Color = new Color (1.0, 1.0, 1.0);
					ctx.Operator = Operator.Source;
					ctx.Paint ();

					var calibratedRect = RecalibrateChildRectangle (bounds);
					// Fill it with one round rectangle
					RoundRectangle (ctx, calibratedRect, 10);
					ctx.Color = new Color (backgroundColor.Red, backgroundColor.Green, backgroundColor.Blue, backgroundColor.Alpha);
					ctx.FillPreserve ();
					ctx.LineWidth = .5;
					ctx.Color = new Color (black.Red, black.Green, black.Blue, black.Alpha);
					ctx.Stroke ();

					// Triangle
					// We first begin by positionning ourselves at the top-center or bottom center of the previous rectangle
					var arrowX = bounds.Center.X;
					var arrowY = arrowPosition == Xwt.Popover.Position.Top ? calibratedRect.Top : calibratedRect.Bottom;
					ctx.NewPath ();
					ctx.MoveTo (arrowX, arrowY);
					// We draw the rectangle path
					DrawTriangle (ctx);
					// We use it
					ctx.Color = new Color (black.Red, black.Green, black.Blue, black.Alpha);
					ctx.StrokePreserve ();
					ctx.ClosePath ();
					ctx.Color = new Color (backgroundColor.Red, backgroundColor.Green, backgroundColor.Blue, backgroundColor.Alpha);
					ctx.Fill ();
				}

				base.OnExposeEvent (evnt);
				return false;
			}
			
			void DrawTriangle (Context ctx)
			{
				var triangleSide = 2 * arrowPadding / Math.Sqrt (3);
				var halfSide = triangleSide / 2;
				var verticalModifier = arrowPosition == Xwt.Popover.Position.Top ? -1 : 1;
				// Move to the left
				ctx.RelMoveTo (-halfSide, 0);
				ctx.RelLineTo (halfSide, verticalModifier * arrowPadding);
				ctx.RelLineTo (halfSide, verticalModifier * -arrowPadding);
			}

			void RoundRectangle (Context ctx, Rectangle rect, double radius)
			{
				var pi = Math.PI;
				var a = rect.Left;
				var b = rect.Right;
				var c = rect.Top;
				var d = rect.Bottom;
				ctx.Arc(a + radius, c + radius, radius, 2*(pi/2), 3*(pi/2));
				ctx.Arc(b - radius, c + radius, radius, 3*(pi/2), 4*(pi/2));
				ctx.Arc(b - radius, d - radius, radius, 0*(pi/2), 1*(pi/2));
				ctx.Arc(a + radius, d - radius, radius, 1*(pi/2), 2*(pi/2));
				ctx.ClosePath ();
			}

			Xwt.Rectangle RecalibrateChildRectangle (Xwt.Rectangle bounds)
			{
				switch (arrowPosition) {
				case Xwt.Popover.Position.Top:
					return new Rectangle (bounds.X, bounds.Y + arrowPadding, bounds.Width, bounds.Height - arrowPadding);
				case Xwt.Popover.Position.Bottom:
					return new Rectangle (bounds.X, bounds.Y, bounds.Width, bounds.Height - arrowPadding);
				}
				return bounds;
			}
		}

		PopoverWindow popover;

		public event EventHandler Closed;

		public void Init (IWindowFrameBackend parent, IWidgetBackend child, Xwt.Popover.Position orientation)
		{
			popover = new PopoverWindow ((Gtk.Widget)child.NativeWidget, orientation);
			popover.TransientFor = ((WindowFrameBackend)parent).Window;
			popover.DestroyWithParent = true;
			popover.Hidden += (o, args) => {
				if (Closed != null)
					Closed (this, EventArgs.Empty);
			};
		}

		public void Run (Point position)
		{
			popover.ShowAll ();
			popover.GrabFocus ();
			int w, h;
			popover.GetSize (out w, out h);
			popover.Move ((int)position.X - w / 2, (int)position.Y);
			popover.SizeAllocated += (o, args) => popover.Move ((int)position.X - args.Allocation.Width / 2, (int)position.Y);
		}

		public void Dispose ()
		{

		}
	}
}